// ReleaseClient.cs: the one place OpenDash talks to the network.
//
// Deliberately small and deliberately dull. ADR 0012 makes exactly two promises about the wire, that a User-Agent
// naming the product is sent and that nothing identifying the user is, and both are pinned by ReleaseClientTests
// against a loopback listener rather than asserted in prose.
//
// HttpWebRequest rather than HttpClient: HttpClient needs a System.Net.Http reference on net48 and buys nothing
// here, since there is one request at a time and no connection pooling to gain. WebClient would hide the headers.
using System;
using System.IO;
using System.Net;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>What a request came back with. A failure carries a reason for the log and nothing for the panel.</summary>
    public sealed class FetchResult
    {
        public bool Ok { get; set; }
        public string Body { get; set; }
        public byte[] Bytes { get; set; }
        public string Reason { get; set; }

        public static FetchResult Failed(string reason) => new FetchResult { Ok = false, Reason = reason };
    }

    /// <summary>Fetching, behind an interface so that what decides can be tested without a socket.</summary>
    public interface IReleaseSource
    {
        FetchResult GetString(string url);

        /// <param name="progress">
        /// How far through the body this is, from 0 to 1, or null for a caller with nothing to draw. Optional
        /// because only the panel has a bar and every other caller would otherwise pass null by hand. An
        /// implementation must not let this decide the fetch: the caller draws it on the UI thread and a settings
        /// page that has closed makes that throw, which is no reason to report a download that arrived as failed.
        /// </param>
        FetchResult GetBytes(string url, Action<double> progress = null);
    }

    public sealed class ReleaseClient : IReleaseSource
    {
        /// <summary>Bounded so that a socket that never answers cannot hold a background thread indefinitely.</summary>
        public const int TimeoutMilliseconds = 15000;

        /// <summary>Enough for the largest package published, with room to spare, so a wrong URL cannot fill a disk.</summary>
        public const int MaxDownloadBytes = 64 * 1024 * 1024;

        private readonly string userAgent;

        /// <param name="productVersion">The plugin's version. The header says what the software is, never who runs it.</param>
        public ReleaseClient(string productVersion)
        {
            userAgent = "openDash/" + (string.IsNullOrWhiteSpace(productVersion) ? "0.0.0" : productVersion.Trim());
        }

        /// <summary>The User-Agent every request carries, which is the whole of what OpenDash discloses about itself.</summary>
        public string UserAgent => userAgent;

        /// <summary>Fetches a text body, for the release listing.</summary>
        public FetchResult GetString(string url)
        {
            return Send(url, response =>
            {
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return new FetchResult { Ok = true, Body = reader.ReadToEnd() };
                }
            });
        }

        /// <summary>Fetches bytes, for a release asset. Follows the redirect GitHub answers with, and says how far
        /// through it is whenever the answer declared how long it would be.</summary>
        public FetchResult GetBytes(string url, Action<double> progress = null)
        {
            return Send(url, response =>
            {
                // Read before the stream is, because the header is what makes a fraction possible at all: an answer
                // sent chunked declares -1, and a download whose length nobody knows can only say it began and ended.
                var length = response.ContentLength;
                using (var stream = response.GetResponseStream())
                using (var buffer = new MemoryStream())
                {
                    var chunk = new byte[81920];
                    var reported = -1;
                    int read;
                    Report(progress, 0, ref reported);
                    while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        if (buffer.Length + read > MaxDownloadBytes)
                        {
                            return FetchResult.Failed("the download is larger than " + MaxDownloadBytes + " bytes");
                        }
                        buffer.Write(chunk, 0, read);
                        if (length > 0) Report(progress, (double)buffer.Length / length, ref reported);
                    }
                    Report(progress, 1, ref reported);
                    return new FetchResult { Ok = true, Bytes = buffer.ToArray() };
                }
            });
        }

        /// <summary>
        /// One report per whole percent, and never a report that can decide the download.
        /// </summary>
        /// <remarks>
        /// The throttle is what keeps the panel usable: a package is a few hundred chunks of 80 KB and every report
        /// crosses to the UI thread through Dispatcher.Invoke, so a report per chunk would spend the download
        /// redrawing a bar that had not visibly moved. A hundred steps is more than a 240 px bar can show anyway.
        ///
        /// The guard is the interface's promise kept: Send turns anything thrown under it into a failed fetch, so a
        /// report that threw because the settings page had closed would be reported as a download that did not
        /// arrive, and the update would stop for a bar nobody was watching.
        /// </remarks>
        private static void Report(Action<double> progress, double fraction, ref int reported)
        {
            if (progress == null) return;
            var percent = (int)(fraction * 100);
            if (percent <= reported) return;
            reported = percent;
            try
            {
                progress(fraction);
            }
            catch
            {
                // Nothing to report and nowhere to report it: this file carries no logger, and a bar nobody is
                // watching is not a fault worth a line in SimHub's log.
            }
        }

        private FetchResult Send(string url, Func<HttpWebResponse, FetchResult> read)
        {
            if (string.IsNullOrWhiteSpace(url)) return FetchResult.Failed("no address to fetch");
            try
            {
                EnsureModernTls();
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                // api.github.com answers 403 without this, and it is the only thing OpenDash says about itself.
                request.UserAgent = userAgent;
                request.Accept = "application/vnd.github+json";
                request.Timeout = TimeoutMilliseconds;
                request.ReadWriteTimeout = TimeoutMilliseconds;
                // An asset answers 302 to a signed URL on another host, which is followed.
                request.AllowAutoRedirect = true;
                // Nothing about the user travels: no credentials, no cookies, no proxy identity.
                request.Credentials = null;
                request.UseDefaultCredentials = false;
                request.CookieContainer = null;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return FetchResult.Failed("the server answered " + (int)response.StatusCode);
                    }
                    return read(response);
                }
            }
            catch (WebException ex)
            {
                var http = ex.Response as HttpWebResponse;
                return FetchResult.Failed(http != null ? "the server answered " + (int)http.StatusCode : ex.Status.ToString());
            }
            catch (Exception ex)
            {
                return FetchResult.Failed(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Widens the process-wide protocol set to include TLS 1.2, which GitHub requires and which net48 does not
        /// always enable by default.
        /// </summary>
        /// <remarks>
        /// Widened with |= and never assigned. This setting belongs to the process, which is SimHub's and is shared
        /// with every other plugin, so assigning it would strip whatever the host or another plugin had enabled.
        /// </remarks>
        private static void EnsureModernTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException)
            {
                // An older framework that does not know the value. The request will fail on its own and be reported
                // as unreachable, which is the correct outcome and not worth a second failure path.
            }
        }
    }
}
