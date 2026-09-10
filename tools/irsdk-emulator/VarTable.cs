using System;
using System.Collections.Generic;
using System.Globalization;

namespace IrsdkEmulator
{
    /// <summary>Definition + live values (+ pin flags) of one variable. Values are kept as doubles.</summary>
    public sealed class VarSlot
    {
        public readonly VarDef Def;
        public readonly double[] Values;
        public readonly bool[] Pinned;

        public VarSlot(VarDef def)
        {
            Def = def;
            Values = new double[def.Count];
            Pinned = new bool[def.Count];
        }
    }

    /// <summary>
    /// The live state of all telemetry variables. Drivers write through <see cref="Set"/> which honours pins,
    /// the scenario/timeline writes through <see cref="Force"/>/<see cref="Pin"/>.
    /// </summary>
    public sealed class VarTable
    {
        private readonly List<VarSlot> _slots = new List<VarSlot>();
        private readonly Dictionary<string, VarSlot> _byName = new Dictionary<string, VarSlot>(StringComparer.Ordinal);

        public IList<VarSlot> Slots => _slots;
        public int Count => _slots.Count;

        public List<VarDef> Defs()
        {
            var l = new List<VarDef>(_slots.Count);
            foreach (var s in _slots) l.Add(s.Def);
            return l;
        }

        public VarSlot Add(VarDef def)
        {
            if (_byName.ContainsKey(def.Name)) throw new ArgumentException("duplicate variable " + def.Name);
            var s = new VarSlot(def);
            _slots.Add(s);
            _byName[def.Name] = s;
            return s;
        }

        public bool Remove(string name)
        {
            if (!_byName.TryGetValue(name, out var s)) return false;
            _byName.Remove(name);
            _slots.Remove(s);
            return true;
        }

        public bool Has(string name) { return _byName.ContainsKey(name); }

        public VarSlot Find(string name)
        {
            return _byName.TryGetValue(name, out var s) ? s : null;
        }

        public double Get(string name, int index = 0)
        {
            if (!_byName.TryGetValue(name, out var s)) return 0;
            if (index < 0 || index >= s.Values.Length) return 0;
            return s.Values[index];
        }

        public bool GetBool(string name, int index = 0) { return Get(name, index) != 0; }

        /// <summary>Driver write: ignored when the element is pinned or the variable does not exist.</summary>
        public bool Set(string name, double value) { return Set(name, 0, value); }

        public bool Set(string name, int index, double value)
        {
            if (!_byName.TryGetValue(name, out var s)) return false;
            if (index < 0 || index >= s.Values.Length) return false;
            if (s.Pinned[index]) return false;
            s.Values[index] = value;
            return true;
        }

        public bool SetBool(string name, bool value) { return Set(name, 0, value ? 1 : 0); }
        public bool SetBool(string name, int index, bool value) { return Set(name, index, value ? 1 : 0); }

        /// <summary>Driver write of all elements (honours pins).</summary>
        public void Fill(string name, double value)
        {
            if (!_byName.TryGetValue(name, out var s)) return;
            for (int i = 0; i < s.Values.Length; i++) if (!s.Pinned[i]) s.Values[i] = value;
        }

        /// <summary>Scenario/timeline write: bypasses pins. index -1 == all elements.</summary>
        public bool Force(string name, int index, double value)
        {
            if (!_byName.TryGetValue(name, out var s)) return false;
            if (index < 0)
            {
                for (int i = 0; i < s.Values.Length; i++) s.Values[i] = value;
                return true;
            }
            if (index >= s.Values.Length) return false;
            s.Values[index] = value;
            return true;
        }

        public bool Pin(string name, int index, bool pinned)
        {
            if (!_byName.TryGetValue(name, out var s)) return false;
            if (index < 0) { for (int i = 0; i < s.Pinned.Length; i++) s.Pinned[i] = pinned; return true; }
            if (index >= s.Pinned.Length) return false;
            s.Pinned[index] = pinned;
            return true;
        }

        /// <summary>Parses "Name" or "Name[7]" into name + index (index -1 == whole variable).</summary>
        public static bool ParseRef(string s, out string name, out int index)
        {
            name = (s ?? "").Trim();
            index = -1;
            int lb = name.IndexOf('[');
            if (lb < 0) return name.Length > 0;
            int rb = name.IndexOf(']', lb);
            if (rb < 0) return false;
            string idx = name.Substring(lb + 1, rb - lb - 1).Trim();
            name = name.Substring(0, lb).Trim();
            if (!int.TryParse(idx, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return false;
            return name.Length > 0;
        }

        /// <summary>Scalar conversion for scenario values: numbers, bools, "0x.." strings and session-flag names ("green|startHidden").</summary>
        public static double ValueToDouble(object v)
        {
            if (v is string s)
            {
                try { return Json.ParseNumber(s); }
                catch (FormatException) { return FlagCycleDriver.ParseFlags(s); }
            }
            return Json.ToDouble(v);
        }

        /// <summary>
        /// Applies a JSON value to a variable reference.
        /// value may be: number/bool/string (scalar: fills the addressed element(s)), array (elements 0..n-1),
        /// or object {"3": v, "12": v} (individual elements).
        /// </summary>
        public bool Apply(string reference, object value, bool pin)
        {
            if (!ParseRef(reference, out var name, out var index)) throw new FormatException("bad variable reference '" + reference + "'");
            if (!_byName.TryGetValue(name, out var s)) return false;
            if (value is List<object> list)
            {
                int start = Math.Max(index, 0);
                for (int i = 0; i < list.Count && start + i < s.Values.Length; i++)
                {
                    s.Values[start + i] = ValueToDouble(list[i]);
                    if (pin) s.Pinned[start + i] = true;
                }
                return true;
            }
            if (value is Dictionary<string, object> obj)
            {
                foreach (var kv in obj)
                {
                    int i = int.Parse(kv.Key.Trim(), CultureInfo.InvariantCulture);
                    if (i < 0 || i >= s.Values.Length) throw new IndexOutOfRangeException(name + "[" + i + "] out of range (count " + s.Values.Length + ")");
                    s.Values[i] = ValueToDouble(kv.Value);
                    if (pin) s.Pinned[i] = true;
                }
                return true;
            }
            double d = ValueToDouble(value);
            if (index >= s.Values.Length) throw new IndexOutOfRangeException(name + "[" + index + "] out of range (count " + s.Values.Length + ")");
            Force(name, index, d);
            if (pin) Pin(name, index, true);
            return true;
        }

        /// <summary>Serialises every variable into a telemetry buffer image (offsets from the layout).</summary>
        public void Serialize(byte[] buffer)
        {
            foreach (var s in _slots)
            {
                var d = s.Def;
                int off = d.Offset;
                var vals = s.Values;
                switch (d.Type)
                {
                    case IrVarType.Char:
                    case IrVarType.Bool:
                        for (int i = 0; i < vals.Length; i++) buffer[off + i] = d.Type == IrVarType.Bool ? (byte)(vals[i] != 0 ? 1 : 0) : (byte)ToInt(vals[i]);
                        break;
                    case IrVarType.Int:
                    case IrVarType.BitField:
                        for (int i = 0; i < vals.Length; i++) LE.WriteInt32(buffer, off + 4 * i, ToInt(vals[i]));
                        break;
                    case IrVarType.Float:
                        for (int i = 0; i < vals.Length; i++) LE.WriteSingle(buffer, off + 4 * i, (float)vals[i]);
                        break;
                    case IrVarType.Double:
                        for (int i = 0; i < vals.Length; i++) LE.WriteDouble(buffer, off + 8 * i, vals[i]);
                        break;
                }
            }
        }

        /// <summary>double -> int32 with wrap-around so that bit fields like 0x80000000 survive.</summary>
        public static int ToInt(double v)
        {
            if (double.IsNaN(v)) return 0;
            if (v >= 4294967296.0 || v <= -2147483649.0) return 0;
            long l = (long)Math.Round(v);
            return unchecked((int)l);
        }

        public string Format(string name, string fmt = "0.###")
        {
            if (!_byName.TryGetValue(name, out var s)) return "--";
            var d = s.Def;
            double v = s.Values[0];
            if (d.Type == IrVarType.Bool) return v != 0 ? "true" : "false";
            if (d.Type == IrVarType.BitField) return "0x" + unchecked((uint)ToInt(v)).ToString("X8");
            if (d.Type == IrVarType.Int) return ToInt(v).ToString(CultureInfo.InvariantCulture);
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }
    }
}
