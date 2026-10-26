using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

class Hid
{
    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
    const int ERROR_IO_PENDING = 997;
    const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;

    [StructLayout(LayoutKind.Sequential)] struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid g; public int flags; public IntPtr res; }
    [StructLayout(LayoutKind.Sequential)] struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID, ProductID, VersionNumber; }
    [StructLayout(LayoutKind.Sequential)] struct HIDP_CAPS {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices, NumberFeatureButtonCaps,
            NumberFeatureValueCaps, NumberFeatureDataIndices;
    }
    [StructLayout(LayoutKind.Sequential)] struct OVERLAPPED { public IntPtr Internal, InternalHigh; public int Offset, OffsetHigh; public IntPtr hEvent; }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(IntPtr h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr p);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, ref HIDP_CAPS c);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr p, int f);
    [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, int i, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref SP_DEVICE_INTERFACE_DATA d, IntPtr det, int sz, out int need, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern SafeFileHandle CreateFile(string n, uint a, uint s, IntPtr sec, uint d, uint f, IntPtr t);
    // Tampon IntPtr: overlapped işlem sürerken GC'nin taşıyamayacağı yönetilmeyen bellek.
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(SafeFileHandle h, IntPtr b, int n, out int w, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadFile(SafeFileHandle h, IntPtr b, int n, out int r, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateEvent(IntPtr s, bool m, bool i, IntPtr n);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr h, int ms);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetOverlappedResult(SafeFileHandle h, IntPtr ov, out int t, bool w);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool CancelIoEx(SafeFileHandle h, IntPtr ov);
    const int ERROR_TIMEOUT = 1460;

    public SafeFileHandle Handle;
    public string Path;
    public ushort Vid, Pid;
    public int LastError; // son başarısız Send'in Win32 hata kodu
    Queue<byte[]> _queue = new Queue<byte[]>();
    object _lock = new object();
    Thread _reader;
    volatile bool _stop;

    public static List<string> Paths()
    {
        var list = new List<string>();
        Guid g; HidD_GetHidGuid(out g);
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        for (int i = 0; ; i++)
        {
            var d = new SP_DEVICE_INTERFACE_DATA(); d.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
            int need;
            SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out need, IntPtr.Zero);
            IntPtr buf = Marshal.AllocHGlobal(need);
            try
            {
                Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(set, ref d, buf, need, out need, IntPtr.Zero)) continue;
                list.Add(Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4)));
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        SetupDiDestroyDeviceInfoList(set);
        return list;
    }

    public static Hid Open(ushort vid, ushort pid, ushort usagePage, ushort usage)
    {
        foreach (var p in Paths())
        {
            SafeFileHandle h = CreateFile(p, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h.IsInvalid) continue;
            var a = new HIDD_ATTRIBUTES(); a.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
            IntPtr pp;
            if (HidD_GetAttributes(h.DangerousGetHandle(), ref a) && HidD_GetPreparsedData(h.DangerousGetHandle(), out pp))
            {
                var c = new HIDP_CAPS(); HidP_GetCaps(pp, ref c); HidD_FreePreparsedData(pp);
                if (a.VendorID == vid && a.ProductID == pid && c.UsagePage == usagePage && c.Usage == usage)
                {
                    var dev = new Hid(); dev.Handle = h; dev.Path = p; dev.Vid = a.VendorID; dev.Pid = a.ProductID;
                    dev.StartReader();
                    return dev;
                }
            }
            h.Close();
        }
        return null;
    }

    void StartReader()
    {
        _reader = new Thread(() => {
            while (!_stop)
            {
                bool failed;
                byte[] r = ReadRaw(500, out failed);
                if (failed) { Thread.Sleep(50); continue; } // anında hata: döngü boşa dönmesin
                if (r == null) continue;
                if (r.Length < 2) continue;
                byte[] data = new byte[r.Length - 1];
                Array.Copy(r, 1, data, 0, data.Length);
                lock (_lock) { _queue.Enqueue(data); while (_queue.Count > 200) _queue.Dequeue(); }
            }
        });
        _reader.IsBackground = true;
        _reader.Start();
    }

    // Bekleyen işlem zaman aşımına uğrarsa yalnızca o işlem iptal edilir ve iptalin
    // bitmesi beklenir (GetOverlappedResult, bWait=true). OVERLAPPED, olay ve tampon
    // ancak Windows onlarla işini bitirdikten sonra serbest bırakılır.
    // Dönüş: işlem başarılıysa true; aktarılan bayt sayısı ve hata kodu out ile.
    bool Overlapped(bool write, IntPtr buf, int len, int timeoutMs, out int done, out int err)
    {
        done = 0; err = 0;
        IntPtr ev = CreateEvent(IntPtr.Zero, true, false, IntPtr.Zero);
        if (ev == IntPtr.Zero) { err = Marshal.GetLastWin32Error(); return false; }
        IntPtr pov = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OVERLAPPED)));
        try
        {
            var ov = new OVERLAPPED(); ov.hEvent = ev; Marshal.StructureToPtr(ov, pov, false);
            bool ok = write ? WriteFile(Handle, buf, len, out done, pov) : ReadFile(Handle, buf, len, out done, pov);
            if (ok) return true;
            err = Marshal.GetLastWin32Error();
            if (err != ERROR_IO_PENDING) return false;
            bool timedOut = WaitForSingleObject(ev, timeoutMs) != 0;
            if (timedOut) CancelIoEx(Handle, pov);
            // İptal edilse de edilmese de işlemin gerçekten bitmesini bekle.
            ok = GetOverlappedResult(Handle, pov, out done, true);
            if (ok) { err = 0; return true; } // iptal anında tamamlanmış olabilir: sonucu kullan
            err = timedOut ? ERROR_TIMEOUT : Marshal.GetLastWin32Error();
            return false;
        }
        catch (ObjectDisposedException) { err = 6; return false; } // ERROR_INVALID_HANDLE: bağlantı kapatıldı
        finally { Marshal.FreeHGlobal(pov); CloseHandle(ev); }
    }

    byte[] ReadRaw(int timeoutMs, out bool failed)
    {
        failed = false;
        IntPtr buf = Marshal.AllocHGlobal(64);
        try
        {
            int read, err;
            if (!Overlapped(false, buf, 64, timeoutMs, out read, out err))
            {
                failed = err != ERROR_TIMEOUT; // zaman aşımı boşta normaldir
                return null;
            }
            byte[] ret = new byte[read];
            Marshal.Copy(buf, ret, 0, read);
            return ret;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public bool Send(byte[] payload63)
    {
        byte[] data = new byte[64]; data[0] = 1;
        Array.Copy(payload63, 0, data, 1, Math.Min(63, payload63.Length));
        IntPtr buf = Marshal.AllocHGlobal(64);
        try
        {
            Marshal.Copy(data, 0, buf, 64);
            int written, err;
            bool ok = Overlapped(true, buf, 64, 2000, out written, out err);
            if (ok && written != 64) { ok = false; err = 0; }
            LastError = ok ? 0 : err;
            return ok;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public void Drain() { lock (_lock) _queue.Clear(); }

    public byte[] Wait(Func<byte[], bool> match, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            byte[] d = null;
            lock (_lock) if (_queue.Count > 0) d = _queue.Dequeue();
            if (d != null && match(d)) return d;
            if (d == null) Thread.Sleep(5);
        }
        return null;
    }

    // Okuma döngüsünü durdur, bekleyen okumayı iptal et, döngünün bitmesini bekle, sonra kapat.
    public void Close()
    {
        _stop = true;
        try { CancelIoEx(Handle, IntPtr.Zero); } catch {}
        if (_reader != null && _reader != Thread.CurrentThread) _reader.Join(1000);
        try { Handle.Close(); } catch {}
    }
}
