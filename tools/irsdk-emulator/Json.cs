using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IrsdkEmulator
{
    /// <summary>
    /// Minimal dependency-free JSON reader.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double,
    /// strings string, true/false bool, null null. Accepts // and /* */ comments and trailing commas
    /// so scenario files can be annotated.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            object v = p.ParseValue();
            p.SkipWs();
            if (!p.AtEnd) throw p.Error("trailing characters after JSON value");
            return v;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            var o = Parse(text) as Dictionary<string, object>;
            if (o == null) throw new FormatException("JSON root must be an object");
            return o;
        }

        // ---- helpers on parsed values -------------------------------------------------------

        public static bool TryGet(Dictionary<string, object> d, string key, out object v)
        {
            v = null;
            return d != null && key != null && d.TryGetValue(key, out v);
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> d, string key)
        {
            return TryGet(d, key, out var v) ? v as Dictionary<string, object> : null;
        }

        public static List<object> GetList(Dictionary<string, object> d, string key)
        {
            return TryGet(d, key, out var v) ? v as List<object> : null;
        }

        public static string GetString(Dictionary<string, object> d, string key, string def = null)
        {
            if (TryGet(d, key, out var v) && v != null) return v is string s ? s : ToText(v);
            return def;
        }

        public static double GetDouble(Dictionary<string, object> d, string key, double def)
        {
            if (TryGet(d, key, out var v) && v != null) return ToDouble(v);
            return def;
        }

        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            return (int)Math.Round(GetDouble(d, key, def));
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            if (TryGet(d, key, out var v) && v != null)
            {
                if (v is bool b) return b;
                return ToDouble(v) != 0;
            }
            return def;
        }

        /// <summary>Converts a parsed JSON scalar to a double. Strings may be decimal or 0x-hex.</summary>
        public static double ToDouble(object v)
        {
            switch (v)
            {
                case null: return 0;
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s: return ParseNumber(s);
                case int i: return i;
                case long l: return l;
                case float f: return f;
                default: return Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
        }

        public static double ParseNumber(string s)
        {
            s = (s ?? "").Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return 0;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            throw new FormatException("cannot convert '" + s + "' to a number");
        }

        public static string ToText(object v)
        {
            switch (v)
            {
                case null: return "";
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Compact serializer (used for --dump output only).</summary>
        public static string Serialize(object v)
        {
            var sb = new StringBuilder();
            Write(sb, v);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case Dictionary<string, object> o:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in o)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        Write(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case List<object> l:
                    sb.Append('[');
                    for (int i = 0; i < l.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Write(sb, l[i]);
                    }
                    sb.Append(']');
                    break;
                default: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- parser ---------------------------------------------------------------------------

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s ?? ""; }

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string msg)
            {
                int line = 1, col = 1;
                for (int k = 0; k < _i && k < _s.Length; k++)
                {
                    if (_s[k] == '\n') { line++; col = 1; } else col++;
                }
                return new FormatException("JSON error at line " + line + ", column " + col + ": " + msg);
            }

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { _i++; continue; }
                    if (c == '/' && _i + 1 < _s.Length)
                    {
                        if (_s[_i + 1] == '/')
                        {
                            _i += 2;
                            while (_i < _s.Length && _s[_i] != '\n') _i++;
                            continue;
                        }
                        if (_s[_i + 1] == '*')
                        {
                            int end = _s.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                            if (end < 0) throw Error("unterminated block comment");
                            _i = end + 2;
                            continue;
                        }
                    }
                    break;
                }
            }

            public object ParseValue()
            {
                SkipWs();
                if (AtEnd) throw Error("unexpected end of input");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || c == '+' || c == '.' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("expected '" + word + "'");
                _i += word.Length;
            }

            private object ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || c == 'x' || c == 'X'
                        || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                        _i++;
                    else break;
                }
                string tok = _s.Substring(start, _i - start);
                try { return Json.ParseNumber(tok); }
                catch (FormatException) { throw Error("invalid number '" + tok + "'"); }
            }

            private string ParseString()
            {
                if (_s[_i] != '"') throw Error("expected string");
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error("bad escape '\\" + e + "'");
                    }
                }
                return sb.ToString();
            }

            private Dictionary<string, object> ParseObject()
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++; // {
                while (true)
                {
                    SkipWs();
                    if (AtEnd) throw Error("unterminated object");
                    if (_s[_i] == '}') { _i++; break; }
                    string key = ParseString();
                    SkipWs();
                    if (AtEnd || _s[_i] != ':') throw Error("expected ':' after key '" + key + "'");
                    _i++;
                    d[key] = ParseValue();
                    SkipWs();
                    if (AtEnd) throw Error("unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; break; }
                    throw Error("expected ',' or '}'");
                }
                return d;
            }

            private List<object> ParseArray()
            {
                var l = new List<object>();
                _i++; // [
                while (true)
                {
                    SkipWs();
                    if (AtEnd) throw Error("unterminated array");
                    if (_s[_i] == ']') { _i++; break; }
                    l.Add(ParseValue());
                    SkipWs();
                    if (AtEnd) throw Error("unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; break; }
                    throw Error("expected ',' or ']'");
                }
                return l;
            }
        }
    }
}
