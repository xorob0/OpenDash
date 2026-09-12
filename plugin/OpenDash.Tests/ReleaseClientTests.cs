// ReleaseClientTests.cs: ADR 0012 makes exactly two promises about what goes over the wire. They are checked here
// against a listener on loopback rather than trusted to prose, because a header added by accident later is
// precisely the kind of change that reads as harmless in a diff.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ReleaseClientTests : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly string prefix;
        private readonly List<NameValueCollection2> seen = new List<NameValueCollection2>();

        /// <summary>The headers of one request, copied out before the listener recycles the context.</summary>
        internal sealed class NameValueCollection2
        {
            public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string this[string name] => Headers.TryGetValue(name, out var value) ? value : null;
        }

        private string answer = "[]";
        private byte[] answerBytes;
        private int status = 200;

        public ReleaseClientTests()
        {
            var port = FreePort();
            prefix = "http://127.0.0.1:" + port + "/";
            listener.Prefixes.Add(prefix);
            listener.Start();
            new Thread(Serve) { IsBackground = true }.Start();
        }

        private static int FreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private void Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch { return; }

                var record = new NameValueCollection2();
                foreach (string key in context.Request.Headers) record.Headers[key] = context.Request.Headers[key];
                lock (seen) seen.Add(record);

                context.Response.StatusCode = status;
                var body = answerBytes ?? Encoding.UTF8.GetBytes(answer);
                context.Response.ContentLength64 = body.Length;
                context.Response.OutputStream.Write(body, 0, body.Length);
                context.Response.OutputStream.Close();
            }
        }

        public void Dispose()
        {
            try { listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
        }

        private NameValueCollection2 LastRequest()
        {
            lock (seen) return seen.Last();
        }

        [Fact]
        public void Every_request_says_what_the_software_is()
        {
            answer = "[{\"tag_name\":\"v9.9.9\"}]";
            var result = new ReleaseClient("0.1.0-rc.3").GetString(prefix);

            Assert.True(result.Ok);
            Assert.Equal("openDash/0.1.0-rc.3", LastRequest()["User-Agent"]);
        }

        /// <summary>The promise that nothing about the user travels. Anything identifying added later fails here.</summary>
        [Fact]
        public void No_request_says_anything_about_who_is_running_it()
        {
            new ReleaseClient("0.1.0-rc.3").GetString(prefix);
            var request = LastRequest();

            foreach (var header in new[] { "Authorization", "Cookie", "From", "X-Forwarded-For", "Proxy-Authorization", "Referer" })
            {
                Assert.Null(request[header]);
            }

            // And the User-Agent carries a version, not a machine, a user or an installation.
            var agent = request["User-Agent"];
            Assert.StartsWith("openDash/", agent);
            Assert.DoesNotContain(Environment.MachineName, agent, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.UserName, agent, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_version_that_is_missing_still_produces_a_usable_agent()
        {
            Assert.Equal("openDash/0.0.0", new ReleaseClient(null).UserAgent);
            Assert.Equal("openDash/0.0.0", new ReleaseClient("   ").UserAgent);
        }

        [Fact]
        public void A_refusal_is_reported_rather_than_thrown()
        {
            status = 404;
            var result = new ReleaseClient("0.1.0").GetString(prefix);

            Assert.False(result.Ok);
            Assert.Contains("404", result.Reason);
            Assert.Null(result.Body);
        }

        [Fact]
        public void An_address_that_answers_nothing_is_reported_rather_than_thrown()
        {
            // A port nothing listens on: the failure path a rig with no network takes.
            var dead = "http://127.0.0.1:" + FreePort() + "/";
            var result = new ReleaseClient("0.1.0").GetString(dead);

            Assert.False(result.Ok);
            Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        }

        [Fact]
        public void Bytes_come_back_whole()
        {
            answerBytes = Enumerable.Range(0, 300000).Select(i => (byte)(i % 251)).ToArray();
            var result = new ReleaseClient("0.1.0").GetBytes(prefix);

            Assert.True(result.Ok);
            Assert.Equal(answerBytes.Length, result.Bytes.Length);
            Assert.Equal(answerBytes, result.Bytes);
        }

        [Fact]
        public void Nothing_is_fetched_from_an_empty_address()
        {
            Assert.False(new ReleaseClient("0.1.0").GetString(null).Ok);
            Assert.False(new ReleaseClient("0.1.0").GetBytes("  ").Ok);
        }
    }
}
