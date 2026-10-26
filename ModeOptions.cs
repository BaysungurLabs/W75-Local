using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace W75Ayarlayici
{
    // Işık modu başına gösterilecek seçenekler (gömülü mod-secenekleri.json). Yalnızca arayüzde gizleme/gösterme ve
    // izin verilen yön değerleri içindir; WriteLight paketini ve profildeki değerleri değiştirmez.
    public class ModeOpt
    {
        public string Name;
        public bool Brightness = true, Speed = true, FullColor = true, FgColor = true, BgColor = true;
        public int[] Dirs = new int[] { 0, 1, 2, 3, 4, 5 };
        public bool Known; // tabloda var mı (yoksa her şey gösterilir)
    }

    public static class ModeOptions
    {
        private static Dictionary<string, object> _root;
        private static bool _loaded;

        private static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            try { _root = new JavaScriptSerializer().DeserializeObject(KeyboardLayout.ReadResource("mod-secenekleri.json")) as Dictionary<string, object>; }
            catch { _root = null; }
        }

        private static object Dig(object o, params string[] path)
        {
            foreach (string k in path)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null || !d.TryGetValue(k, out o)) return null;
            }
            return o;
        }

        private static bool Flag(object e, string name, bool def)
        {
            object v = Dig(e, name);
            return v is bool ? (bool)v : def;
        }

        public static ModeOpt Get(int mode)
        {
            Ensure();
            ModeOpt r = new ModeOpt();
            object e = Dig(_root, "modlar", mode.ToString());
            if (e == null) return r; // bilinmeyen mod: hepsi görünür, yönler 0-5
            r.Known = true;
            r.Name = Dig(e, "ad") as string;
            r.Brightness = Flag(e, "parlaklik", true);
            r.Speed = Flag(e, "hiz", true);
            r.FullColor = Flag(e, "tamRenk", true);
            r.FgColor = Flag(e, "onRenk", true);
            r.BgColor = Flag(e, "arkaRenk", true);
            IEnumerable list = Dig(e, "yonler") as IEnumerable;
            List<int> dirs = new List<int>();
            if (list != null)
                foreach (object o in list)
                {
                    int v;
                    if (o != null && int.TryParse(o.ToString(), out v)) dirs.Add(v);
                }
            r.Dirs = dirs.ToArray();
            return r;
        }

        public static bool AllowsDirection(int mode, int value)
        {
            return Array.IndexOf(Get(mode).Dirs, value) >= 0;
        }
    }
}
