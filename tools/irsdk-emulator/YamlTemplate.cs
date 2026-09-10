using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace IrsdkEmulator
{
    /// <summary>
    /// The session-info YAML is a text template with {{placeholders}}. Values come from the scenario ("yaml" object),
    /// from timeline events and from drivers (e.g. the field driver renders ResultsPositions).
    /// Whenever the rendered text changes the emulator rewrites the session string and bumps sessionInfoUpdate.
    /// </summary>
    public sealed class YamlTemplate
    {
        private static readonly Regex Placeholder = new Regex(@"\{\{\s*([A-Za-z0-9_.\-]+)\s*\}\}", RegexOptions.Compiled);

        private string _template = "";
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        private bool _dirty = true;
        private string _lastRendered;

        public string Path { get; private set; }
        public Action<string> Log = s => { };

        public void LoadFile(string path)
        {
            SetTemplate(File.ReadAllText(path));
            Path = path;
        }

        public void SetTemplate(string text)
        {
            _template = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            _dirty = true;
        }

        public void Set(string key, string value)
        {
            value = value ?? "";
            if (_values.TryGetValue(key, out var old) && old == value) return;
            _values[key] = value;
            _dirty = true;
        }

        public string Get(string key)
        {
            return _values.TryGetValue(key, out var v) ? v : null;
        }

        public IEnumerable<KeyValuePair<string, string>> Values => _values;

        public bool Dirty => _dirty;

        /// <summary>Re-renders when dirty. Returns true when the rendered YAML differs from the previous render.</summary>
        public bool RenderIfChanged(out string yaml)
        {
            if (!_dirty) { yaml = _lastRendered; return false; }
            string r = Render();
            bool changed = r != _lastRendered;
            _lastRendered = r;
            yaml = r;
            return changed;
        }

        public string Render()
        {
            _dirty = false;
            string r = Placeholder.Replace(_template, m =>
            {
                string key = m.Groups[1].Value;
                if (_values.TryGetValue(key, out var v)) return v;
                if (_warned.Add(key)) Log("yaml: placeholder {{" + key + "}} has no value, rendering empty");
                return "";
            });
            // iRacing terminates the YAML document with "...\n"; make sure we end with a newline.
            if (!r.EndsWith("\n")) r += "\n";
            return r;
        }

        public List<string> Placeholders()
        {
            var l = new List<string>();
            foreach (Match m in Placeholder.Matches(_template)) if (!l.Contains(m.Groups[1].Value)) l.Add(m.Groups[1].Value);
            return l;
        }
    }
}
