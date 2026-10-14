// Pages.cs: the live values of the four pages, addressed by the names SimHub publishes them under.
//
// A value is named as it appears after DataCorePlugin.GameRawData. in SimHub: Physics.Rpms,
// Graphics.Flags, StaticInfo.CarModel, and an array element as Physics.TyreWear[2] (SimHub numbers it
// Physics.TyreWear03, one-based). So a scenario reads like the property list a dashboard author sees.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using IrsdkEmulator;

namespace AcsmEmulator
{
    public sealed class Pages
    {
        public const string PhysicsMap = @"Local\acpmf_physics";
        public const string GraphicsMap = @"Local\acpmf_graphics";
        public const string StaticMap = @"Local\acpmf_static";
        public const string OpponentsMap = @"Local\acpmf_simhub_v2";

        /// <summary>The pages a value can be addressed in, boxed so reflection writes the one copy.</summary>
        private readonly Dictionary<string, object> _boxes = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>Pinned elements as "Page.Field" (every element) or "Page.Field[i]".</summary>
        private readonly HashSet<string> _pinned = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>SimHub's AC app page. Only published when the scenario has a field to put in it.</summary>
        public SPageFileCrewChief Opponents;

        public Pages()
        {
            _boxes["Physics"] = NewPhysics();
            _boxes["Graphics"] = NewGraphics();
            _boxes["StaticInfo"] = NewStatic();
            Opponents = NewOpponents();
        }

        public Physics Physics => (Physics)_boxes["Physics"];
        public Graphics Graphics => (Graphics)_boxes["Graphics"];
        public StaticInfo StaticInfo => (StaticInfo)_boxes["StaticInfo"];

        public static Physics NewPhysics()
        {
            return new Physics
            {
                Velocity = new float[3], AccG = new float[3], WheelSlip = new float[4], WheelLoad = new float[4],
                WheelsPressure = new float[4], WheelAngularSpeed = new float[4], TyreWear = new float[4],
                TyreDirtyLevel = new float[4], TyreCoreTemperature = new float[4], CamberRad = new float[4],
                SuspensionTravel = new float[4], CarDamage = new float[5], RideHeight = new float[2],
                LocalAngularVelocity = new float[3], BrakeTemp = new float[4], TyreTempI = new float[4],
                TyreTempM = new float[4], TyreTempO = new float[4], TyreContactPoint = new Coordinates[4],
                TyreContactNormal = new Coordinates[4], TyreContactHeading = new Coordinates[4], LocalVelocity = new float[3],
            };
        }

        public static Graphics NewGraphics()
        {
            return new Graphics
            {
                CurrentTime = "", LastTime = "", BestTime = "", Split = "", TyreCompound = "", CarCoordinates = new float[3],
            };
        }

        public static StaticInfo NewStatic()
        {
            return new StaticInfo
            {
                SMVersion = "", ACVersion = "", CarModel = "", Track = "", PlayerName = "", PlayerSurname = "", PlayerNick = "",
                SuspensionMaxTravel = new float[4], TyreRadius = new float[4], TrackConfiguration = "", carSkin = "",
            };
        }

        public static acsVehicleInfo NewVehicle()
        {
            return new acsVehicleInfo
            {
                driverName = new byte[64], carModel = new byte[64], suspensionDamage = new float[4], tyreInflation = new float[4],
            };
        }

        public static SPageFileCrewChief NewOpponents()
        {
            var o = new SPageFileCrewChief
            {
                serverName = new byte[512], vehicle = new acsVehicleInfo[128], acInstallPath = new byte[512], pluginVersion = new byte[32],
            };
            for (int i = 0; i < o.vehicle.Length; i++) o.vehicle[i] = NewVehicle();
            return o;
        }

        /// <summary>Writes a string into a fixed byte field as SimHub's app does: UTF-8, NUL padded.</summary>
        public static void PutBytes(byte[] field, string text)
        {
            Array.Clear(field, 0, field.Length);
            var b = Encoding.UTF8.GetBytes(text ?? "");
            Array.Copy(b, field, Math.Min(b.Length, field.Length - 1));
        }

        // ------------------------------------------------------------------------- addressing

        public sealed class Ref
        {
            public string Page;
            public FieldInfo Field;
            /// <summary>-1 for the whole field.</summary>
            public int Index = -1;
            public string Key => Page + "." + Field.Name;
        }

        /// <summary>Parses "Physics.TyreWear[2]"; null when the page or the field does not exist.</summary>
        public Ref Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int dot = path.IndexOf('.');
            if (dot <= 0) return null;
            string page = path.Substring(0, dot), rest = path.Substring(dot + 1);
            int index = -1;
            int br = rest.IndexOf('[');
            if (br >= 0)
            {
                if (!rest.EndsWith("]")) return null;
                if (!int.TryParse(rest.Substring(br + 1, rest.Length - br - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return null;
                rest = rest.Substring(0, br);
            }
            if (!_boxes.TryGetValue(page, out var box)) return null;
            var f = box.GetType().GetField(rest, BindingFlags.Public | BindingFlags.Instance)
                    ?? box.GetType().GetField(rest, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f == null) return null;
            if (index >= 0)
            {
                if (!f.FieldType.IsArray) return null;
                var arr = (Array)f.GetValue(box);
                if (index >= arr.Length) return null;
            }
            return new Ref { Page = page, Field = f, Index = index };
        }

        public bool IsPinned(Ref r, int index)
        {
            return _pinned.Contains(r.Key) || (index >= 0 && _pinned.Contains(r.Key + "[" + index + "]"));
        }

        /// <summary>Pins or releases an element, or the whole field.</summary>
        public bool Pin(string path, bool on)
        {
            var r = Resolve(path);
            if (r == null) return false;
            string key = r.Index >= 0 ? r.Key + "[" + r.Index + "]" : r.Key;
            if (on) _pinned.Add(key);
            else
            {
                _pinned.Remove(key);
                if (r.Index < 0) _pinned.RemoveWhere(k => k.StartsWith(r.Key + "[", StringComparison.Ordinal));
            }
            return true;
        }

        /// <summary>
        /// Writes a scenario value: a number, a boolean, a string (an enum's name, with or without its AC_
        /// prefix, or the text of a string field), or a list for an array. Unknown names return false. A
        /// value written this way ignores pins: it is the scenario speaking, not a driver.
        /// </summary>
        public bool Apply(string path, object value, bool pin)
        {
            var r = Resolve(path);
            if (r == null) return false;
            var box = _boxes[r.Page];
            var t = r.Field.FieldType;
            if (t.IsArray)
            {
                var arr = (Array)r.Field.GetValue(box);
                if (r.Index >= 0) arr.SetValue(Convert(t.GetElementType(), value, path), r.Index);
                else if (value is List<object> list)
                {
                    for (int i = 0; i < list.Count && i < arr.Length; i++) arr.SetValue(Convert(t.GetElementType(), list[i], path), i);
                }
                else for (int i = 0; i < arr.Length; i++) arr.SetValue(Convert(t.GetElementType(), value, path), i);
            }
            else
            {
                if (r.Index >= 0) return false;
                r.Field.SetValue(box, Convert(t, value, path));
            }
            if (pin) Pin(path, true);
            return true;
        }

        /// <summary>What a driver writes: a number, unless the element is pinned.</summary>
        public void Set(string path, double value)
        {
            var r = Resolve(path) ?? throw new ArgumentException("no such value " + path);
            SetRef(r, r.Index, value);
        }

        public void Set(string path, int index, double value)
        {
            var r = Resolve(path) ?? throw new ArgumentException("no such value " + path);
            SetRef(r, index, value);
        }

        private void SetRef(Ref r, int index, double value)
        {
            if (IsPinned(r, index)) return;
            var box = _boxes[r.Page];
            var t = r.Field.FieldType;
            if (t.IsArray)
            {
                var arr = (Array)r.Field.GetValue(box);
                if (index < 0) { for (int i = 0; i < arr.Length; i++) if (!IsPinned(r, i)) arr.SetValue(Convert(t.GetElementType(), value, r.Key), i); }
                else arr.SetValue(Convert(t.GetElementType(), value, r.Key), index);
            }
            else r.Field.SetValue(box, Convert(t, value, r.Key));
        }

        public void SetText(string path, string text)
        {
            var r = Resolve(path) ?? throw new ArgumentException("no such value " + path);
            if (IsPinned(r, -1)) return;
            r.Field.SetValue(_boxes[r.Page], text ?? "");
        }

        public double Get(string path)
        {
            var r = Resolve(path) ?? throw new ArgumentException("no such value " + path);
            return Get(r, r.Index);
        }

        public double Get(string path, int index)
        {
            var r = Resolve(path) ?? throw new ArgumentException("no such value " + path);
            return Get(r, index);
        }

        private double Get(Ref r, int index)
        {
            object v = r.Field.GetValue(_boxes[r.Page]);
            if (v is Array a) v = a.GetValue(index < 0 ? 0 : index);
            return ToDouble(v);
        }

        public string GetText(string path)
        {
            var r = Resolve(path) ?? throw new ArgumentException("no such value " + path);
            object v = r.Field.GetValue(_boxes[r.Page]);
            if (v is Array a) v = a.GetValue(r.Index < 0 ? 0 : r.Index);
            return v is string s ? s : System.Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object v)
        {
            switch (v)
            {
                case null: return 0;
                case bool b: return b ? 1 : 0;
                case Enum e: return System.Convert.ToInt32(e, CultureInfo.InvariantCulture);
                case string s: return Json.ParseNumber(s);
                default: return System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
            }
        }

        private static object Convert(Type t, object value, string path)
        {
            if (t == typeof(string)) return value == null ? "" : (value is string s ? s : Json.ToText(value));
            if (t.IsEnum)
            {
                if (value is string name && name.Length > 0 && !char.IsDigit(name[0]) && name[0] != '-')
                {
                    string full = name.StartsWith("AC_", StringComparison.OrdinalIgnoreCase) ? name : "AC_" + name;
                    try { return Enum.Parse(t, full, true); }
                    catch (ArgumentException) { throw new FormatException(path + ": " + name + " is not one of " + string.Join(", ", Enum.GetNames(t))); }
                }
                return Enum.ToObject(t, (int)Math.Round(Json.ToDouble(value)));
            }
            double d = Json.ToDouble(value);
            if (t == typeof(int)) return (int)Math.Round(d);
            if (t == typeof(float)) return (float)d;
            if (t == typeof(bool)) return d != 0;
            if (t == typeof(byte)) return (byte)d;
            throw new FormatException(path + " is a " + t.Name + ", which a scenario cannot set");
        }

        // ------------------------------------------------------------------------- bytes

        public static int SizeOf(string map)
        {
            switch (map)
            {
                case PhysicsMap: return Marshal.SizeOf(typeof(Physics));
                case GraphicsMap: return Marshal.SizeOf(typeof(Graphics));
                case StaticMap: return Marshal.SizeOf(typeof(StaticInfo));
                case OpponentsMap: return Marshal.SizeOf(typeof(SPageFileCrewChief));
                default: throw new ArgumentException(map);
            }
        }

        public byte[] Bytes(string map)
        {
            switch (map)
            {
                case PhysicsMap: return ToBytes(_boxes["Physics"]);
                case GraphicsMap: return ToBytes(_boxes["Graphics"]);
                case StaticMap: return ToBytes(_boxes["StaticInfo"]);
                case OpponentsMap: return ToBytes(Opponents);
                default: throw new ArgumentException(map);
            }
        }

        public static byte[] ToBytes(object structure)
        {
            var buf = new byte[Marshal.SizeOf(structure.GetType())];
            var h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try { Marshal.StructureToPtr(structure, h.AddrOfPinnedObject(), false); }
            finally { h.Free(); }
            return buf;
        }

        /// <summary>Reads bytes back the way SimHub's SharedMemoryReader does.</summary>
        public static T FromBytes<T>(byte[] buf)
        {
            var h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try { return (T)Marshal.PtrToStructure(h.AddrOfPinnedObject(), typeof(T)); }
            finally { h.Free(); }
        }

        /// <summary>Every addressable scalar and array, for --dump.</summary>
        public IEnumerable<string> Describe()
        {
            foreach (var kv in _boxes)
            {
                foreach (var f in kv.Value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object v = f.GetValue(kv.Value);
                    string text;
                    if (v is Array a)
                    {
                        var parts = new List<string>();
                        foreach (var e in a) parts.Add(e is float fl ? fl.ToString("0.###", CultureInfo.InvariantCulture) : System.Convert.ToString(e, CultureInfo.InvariantCulture));
                        text = "[" + string.Join(", ", parts) + "]";
                    }
                    else text = v is float fl ? fl.ToString("0.###", CultureInfo.InvariantCulture) : System.Convert.ToString(v, CultureInfo.InvariantCulture);
                    string pin = _pinned.Contains(kv.Key + "." + f.Name) ? " [pinned]" : "";
                    yield return string.Format(CultureInfo.InvariantCulture, "{0,-36} {1}{2}", kv.Key + "." + f.Name, text, pin);
                }
            }
        }
    }
}
