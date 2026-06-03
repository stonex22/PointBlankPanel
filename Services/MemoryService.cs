using System.Runtime.InteropServices;
using PointBlankPanel.Helpers;

namespace PointBlankPanel.Services;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MODULEENTRY32
{
    public uint dwSize;
    public uint th32ModuleID;
    public uint th32ProcessID;
    public uint GlblcntUsage;
    public uint ProccntUsage;
    public IntPtr modBaseAddr;
    public uint modBaseSize;
    public IntPtr hModule;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szModule;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szExePath;
}

[StructLayout(LayoutKind.Sequential)]
public struct TOKEN_PRIVILEGES
{
    public uint PrivilegeCount;
    public long Luid;
    public uint Attributes;
}

internal static class Api
{
    private static readonly IntPtr _k32, _a32, _n32;
    private static readonly Dictionary<string, IntPtr> _cache = [];

    private static readonly string
        _sOp, _sWpm, _sRpm, _sVpe, _sVae, _sCrt, _sCtss, _sM32f, _sM32n,
        _sVfe, _sOpt, _sLpv, _sAtp, _sLlw, _sFlEx, _sFl, _sLdr;

    static Api()
    {
        _k32 = GetModuleHandle(StrObf.Get(
            new byte[] { 0x10, 0x1E, 0x09, 0x15, 0x1E, 0x17, 0x48, 0x49, 0x55, 0x1F, 0x17, 0x17 }));
        _a32 = GetModuleHandle(StrObf.Get(
            new byte[] { 0x1A, 0x1F, 0x0D, 0x1A, 0x0B, 0x12, 0x48, 0x49, 0x55, 0x1F, 0x17, 0x17 }));
        _n32 = GetModuleHandle(StrObf.Get(
            new byte[] { 0x15, 0x0F, 0x1F, 0x17, 0x17, 0x55, 0x1F, 0x17, 0x17 }));

        _sOp = StrObf.Get(new byte[] { 0x34, 0x0B, 0x1E, 0x15, 0x2B, 0x09, 0x14, 0x18, 0x1E, 0x08, 0x08 });
        _sWpm = StrObf.Get(new byte[] { 0x2C, 0x09, 0x12, 0x0F, 0x1E, 0x2B, 0x09, 0x14, 0x18, 0x1E, 0x08, 0x08, 0x36, 0x1E, 0x16, 0x14, 0x09, 0x02 });
        _sRpm = StrObf.Get(new byte[] { 0x29, 0x1E, 0x1A, 0x1F, 0x2B, 0x09, 0x14, 0x18, 0x1E, 0x08, 0x08, 0x36, 0x1E, 0x16, 0x14, 0x09, 0x02 });
        _sVpe = StrObf.Get(new byte[] { 0x2D, 0x12, 0x09, 0x0F, 0x0E, 0x1A, 0x17, 0x2B, 0x09, 0x14, 0x0F, 0x1E, 0x18, 0x0F, 0x3E, 0x03 });
        _sVae = StrObf.Get(new byte[] { 0x2D, 0x12, 0x09, 0x0F, 0x0E, 0x1A, 0x17, 0x3A, 0x17, 0x17, 0x14, 0x18, 0x3E, 0x03 });
        _sCrt = StrObf.Get(new byte[] { 0x38, 0x09, 0x1E, 0x1A, 0x0F, 0x1E, 0x29, 0x1E, 0x16, 0x14, 0x0F, 0x1E, 0x2F, 0x13, 0x09, 0x1E, 0x1A, 0x1F });
        _sCtss = StrObf.Get(new byte[] { 0x38, 0x09, 0x1E, 0x1A, 0x0F, 0x1E, 0x2F, 0x14, 0x14, 0x17, 0x13, 0x1E, 0x17, 0x0B, 0x48, 0x49, 0x28, 0x15, 0x1A, 0x0B, 0x08, 0x13, 0x14, 0x0F });
        _sM32f = StrObf.Get(new byte[] { 0x36, 0x14, 0x1F, 0x0E, 0x17, 0x1E, 0x48, 0x49, 0x3D, 0x12, 0x09, 0x08, 0x0F });
        _sM32n = StrObf.Get(new byte[] { 0x36, 0x14, 0x1F, 0x0E, 0x17, 0x1E, 0x48, 0x49, 0x35, 0x1E, 0x03, 0x0F });
        _sVfe = StrObf.Get(new byte[] { 0x2D, 0x12, 0x09, 0x0F, 0x0E, 0x1A, 0x17, 0x3D, 0x09, 0x1E, 0x1E, 0x3E, 0x03 });
        _sOpt = StrObf.Get(new byte[] { 0x34, 0x0B, 0x1E, 0x15, 0x2B, 0x09, 0x14, 0x18, 0x1E, 0x08, 0x08, 0x2F, 0x14, 0x10, 0x1E, 0x15 });
        _sLpv = StrObf.Get(new byte[] { 0x37, 0x14, 0x14, 0x10, 0x0E, 0x0B, 0x2B, 0x09, 0x12, 0x0D, 0x12, 0x17, 0x1E, 0x1C, 0x1E, 0x2D, 0x1A, 0x17, 0x0E, 0x1E, 0x3A });
        _sAtp = StrObf.Get(new byte[] { 0x3A, 0x1F, 0x11, 0x0E, 0x08, 0x0F, 0x2F, 0x14, 0x10, 0x1E, 0x15, 0x2B, 0x09, 0x12, 0x0D, 0x12, 0x17, 0x1E, 0x1C, 0x1E, 0x08 });
        _sLlw = StrObf.Get(new byte[] { 0x37, 0x14, 0x1A, 0x1F, 0x37, 0x12, 0x19, 0x09, 0x1A, 0x09, 0x02, 0x2C });
        _sFlEx = StrObf.Get(new byte[] { 0x3D, 0x09, 0x1E, 0x1E, 0x37, 0x12, 0x19, 0x09, 0x1A, 0x09, 0x02, 0x3A, 0x15, 0x1F, 0x1E, 0x0F, 0x2F, 0x13, 0x09, 0x1E, 0x1A, 0x1F });
        _sFl = StrObf.Get(new byte[] { 0x3D, 0x09, 0x1E, 0x1E, 0x37, 0x12, 0x19, 0x09, 0x1A, 0x09, 0x02 });
        _sLdr = StrObf.Get(new byte[] { 0x37, 0x1F, 0x09, 0x02, 0x15, 0x15, 0x14, 0x1A, 0x1F, 0x3D, 0x17, 0x17 });
    }

    private delegate int ThreadStart(IntPtr p);

    private static IntPtr Resolve(string mod, string name)
    {
        var key = $"{mod}:{name}";
        if (_cache.TryGetValue(key, out var a)) return a;
        var hm = mod switch { "k" => _k32, "a" => _a32, "n" => _n32, _ => IntPtr.Zero };
        a = GetProcAddress(hm, name);
        _cache[key] = a;
        return a;
    }

    private static T Fn<T>(string mod, string name) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(Resolve(mod, name));

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr h, string n);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string n);

    [DllImport("kernel32.dll")]
    public static extern int GetLastError();

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll")]
    public static extern int WaitForSingleObject(IntPtr h, int ms);

    [DllImport("kernel32.dll")]
    public static extern bool GetExitCodeThread(IntPtr h, out int c);

    [DllImport("ntdll.dll")]
    public static extern int RtlCreateUserThread(IntPtr h, IntPtr ts, bool cs, int zb, IntPtr sr, IntPtr sc, IntPtr sa, IntPtr p, out IntPtr ht, out int cid);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr DelOpenProcess(uint a, bool b, int pid);
    public static IntPtr OpenProcess(uint a, bool b, int pid)
        => Fn<DelOpenProcess>("k", _sOp)(a, b, pid);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelWPM(IntPtr h, IntPtr a, byte[] b, int s, out int w);
    public static bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, int s, out int w)
        => Fn<DelWPM>("k", _sWpm)(h, a, b, s, out w);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelRPM(IntPtr h, IntPtr a, byte[] b, int s, out int r);
    public static bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, int s, out int r)
        => Fn<DelRPM>("k", _sRpm)(h, a, b, s, out r);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelVPE(IntPtr h, IntPtr a, int s, uint p, out uint o);
    public static bool VirtualProtectEx(IntPtr h, IntPtr a, int s, uint p, out uint o)
        => Fn<DelVPE>("k", _sVpe)(h, a, s, p, out o);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr DelVAE(IntPtr h, IntPtr a, int s, uint t, uint p);
    public static IntPtr VirtualAllocEx(IntPtr h, IntPtr a, int s, uint t, uint p)
        => Fn<DelVAE>("k", _sVae)(h, a, s, t, p);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr DelCRT(IntPtr h, IntPtr a, int s, IntPtr f, IntPtr p, uint f2, out int id);
    public static IntPtr CreateRemoteThread(IntPtr h, IntPtr a, int s, IntPtr f, IntPtr p, uint f2, out int id)
        => Fn<DelCRT>("k", _sCrt)(h, a, s, f, p, f2, out id);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr DelCTSS(uint f, uint pid);
    public static IntPtr CreateToolhelp32Snapshot(uint f, uint pid)
        => Fn<DelCTSS>("k", _sCtss)(f, pid);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelM32F(IntPtr s, ref MODULEENTRY32 m);
    public static bool Module32First(IntPtr s, ref MODULEENTRY32 m)
        => Fn<DelM32F>("k", _sM32f)(s, ref m);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelM32N(IntPtr s, ref MODULEENTRY32 m);
    public static bool Module32Next(IntPtr s, ref MODULEENTRY32 m)
        => Fn<DelM32N>("k", _sM32n)(s, ref m);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelVFE(IntPtr h, IntPtr a, int s, uint t);
    public static bool VirtualFreeEx(IntPtr h, IntPtr a, int s, uint t)
        => Fn<DelVFE>("k", _sVfe)(h, a, s, t);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelOPT(IntPtr h, uint d, out IntPtr t);
    public static bool OpenProcessToken(IntPtr h, uint d, out IntPtr t)
        => Fn<DelOPT>("a", _sOpt)(h, d, out t);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelLPV(string? s, string n, out long l);
    public static bool LookupPrivilegeValue(string? s, string n, out long l)
        => Fn<DelLPV>("a", _sLpv)(s, n, out l);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool DelATP(IntPtr t, bool d, ref TOKEN_PRIVILEGES s, int l, IntPtr p, IntPtr r);
    public static bool AdjustTokenPrivileges(IntPtr t, bool d, ref TOKEN_PRIVILEGES s, int l, IntPtr p, IntPtr r)
        => Fn<DelATP>("a", _sAtp)(t, d, ref s, l, p, r);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr DelLLW(string p);
    public static IntPtr LoadLibraryW(string p)
        => Fn<DelLLW>("k", _sLlw)(p);

    public static IntPtr AddressOfLoadLibraryW => Resolve("k", _sLlw);
    public static IntPtr AddressOfFreeLibraryAndExitThread => Resolve("k", _sFlEx);
    public static IntPtr AddressOfFreeLibrary => Resolve("k", _sFl);
    public static IntPtr AddressOfLdrUnloadDll => Resolve("n", _sLdr);
}

public class MemoryService : IDisposable
{
    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPMODULE32 = 0x00000010;
    private const uint PROCESS_ALL = 0x001F0FFF;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_SET_QUOTA = 0x0100;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;

    private IntPtr _handle = IntPtr.Zero;
    private int _processId;
    private string _processName = "";

    public bool IsConnected => _handle != IntPtr.Zero;
    public string ProcessName => _processName;
    public int ProcessId => _processId;
    public int LastErrorCode { get; private set; }

    // System Helpers
    [DllImport("ntdll.dll")]
    private static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uPeriod);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

    public static string SetTimerResolution500us()
    {
        int status = NtSetTimerResolution(5000, true, out var current);
        if (status == 0)
            return "✔ Timer Resolution: 0.5ms ativado!";

        status = NtSetTimerResolution(10000, true, out current);
        if (status == 0)
            return "✔ Timer Resolution: 1ms ativado (0.5ms indisponível).";

        uint mm = timeBeginPeriod(1);
        if (mm == 0)
            return "✔ Timer Resolution: 1ms (timeBeginPeriod).";

        return "✘ Falha ao definir Timer Resolution (NTSTATUS 0x" + status.ToString("X8") + ").";
    }

    public static string RestoreTimerResolution()
    {
        NtSetTimerResolution(10000, true, out _);
        timeEndPeriod(1);
        return "✘ Timer Resolution restaurado.";
    }

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    public static bool EmptyWorkingSetByPid(int pid)
    {
        IntPtr h = Api.OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, pid);
        if (h == IntPtr.Zero) return false;
        bool ok = EmptyWorkingSet(h);
        CloseHandle(h);
        return ok;
    }

    public static string CleanStandbyList()
    {
        int type = 4;
        int size = Marshal.SizeOf<int>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(type, ptr, false);

        int status = NtSetSystemInformation(0x50, ptr, size);

        if (status == 0)
        {
            Marshal.FreeHGlobal(ptr);
            return "✔ Memória limpa! Lista de espera removida (NtSetSystemInformation).";
        }

        Marshal.FreeHGlobal(ptr);

        // Fallback: EmptyWorkingSet em todos os processos
        int count = 0;
        foreach (var proc in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (EmptyWorkingSetByPid(proc.Id))
                    count++;
            }
            catch { }
        }
        return "✔ Memória limpa! Working sets de " + count + " processos reduzidos.";
    }

    public bool Connect(string processName)
    {
        Disconnect();
        var procs = System.Diagnostics.Process.GetProcessesByName(processName.Replace(".exe", ""));
        if (procs.Length == 0) return false;
        var proc = procs[0];
        _handle = Api.OpenProcess(PROCESS_ALL, false, proc.Id);
        if (_handle != IntPtr.Zero)
        { _processId = proc.Id; _processName = proc.ProcessName; return true; }
        return false;
    }

    public bool ConnectAny(params string[] names)
    {
        foreach (var n in names)
            if (Connect(n)) return true;
        return false;
    }

    public void Disconnect()
    {
        if (_handle != IntPtr.Zero) { Api.CloseHandle(_handle); _handle = IntPtr.Zero; }
        _processId = 0; _processName = "";
    }

    public IntPtr? GetModuleBaseAddress(string moduleName)
    {
        if (_handle == IntPtr.Zero) return null;
        moduleName = moduleName.ToLowerInvariant().Replace(".exe", "").Replace(".dll", "");
        var r = TryGetModule(TH32CS_SNAPMODULE32, moduleName);
        if (r != null) return r;
        r = TryGetModule(TH32CS_SNAPMODULE, moduleName);
        if (r != null) return r;
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(_processId);
            foreach (System.Diagnostics.ProcessModule mod in proc.Modules)
            {
                var name = mod.ModuleName.ToLowerInvariant().Replace(".exe", "");
                if (name == moduleName || name == moduleName + ".exe") return mod.BaseAddress;
            }
        }
        catch { }
        return null;
    }

    private IntPtr? TryGetModule(uint flag, string moduleName)
    {
        var snap = Api.CreateToolhelp32Snapshot(flag, (uint)_processId);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return null;
        try
        {
            var e = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
            if (Api.Module32First(snap, ref e))
            {
                do
                {
                    var mn = e.szModule.ToLowerInvariant().Replace(".exe", "").Replace(".dll", "");
                    if (mn == moduleName || e.szExePath.ToLowerInvariant().Contains(moduleName))
                        return e.modBaseAddr;
                } while (Api.Module32Next(snap, ref e));
            }
        }
        finally { Api.CloseHandle(snap); }
        return null;
    }

    public bool WriteBytes(IntPtr address, byte[] data)
    {
        if (_handle == IntPtr.Zero || data.Length == 0) return false;
        Api.VirtualProtectEx(_handle, address, data.Length, PAGE_EXECUTE_READWRITE, out var old);
        var ok = Api.WriteProcessMemory(_handle, address, data, data.Length, out _);
        if (!ok) LastErrorCode = Api.GetLastError();
        Api.VirtualProtectEx(_handle, address, data.Length, old, out _);
        return ok;
    }

    public bool WriteModuleOffset(string moduleName, string offsetHex, byte[] data)
    {
        var baseAddr = GetModuleBaseAddress(moduleName);
        if (baseAddr == null) return false;
        var offset = long.Parse(offsetHex.Replace("0x", "").Replace("0X", ""), System.Globalization.NumberStyles.HexNumber);
        return WriteBytes(new IntPtr(baseAddr.Value.ToInt64() + offset), data);
    }

    public byte[] ReadModuleOffset(string moduleName, string offsetHex, int size)
    {
        var baseAddr = GetModuleBaseAddress(moduleName);
        if (baseAddr == null) return [];
        var offset = long.Parse(offsetHex.Replace("0x", "").Replace("0X", ""), System.Globalization.NumberStyles.HexNumber);
        return ReadBytes(new IntPtr(baseAddr.Value.ToInt64() + offset), size);
    }

    public byte[] ReadBytes(IntPtr address, int size)
    {
        if (_handle == IntPtr.Zero || size <= 0) return [];
        var buf = new byte[size];
        Api.ReadProcessMemory(_handle, address, buf, size, out _);
        return buf;
    }

    public int? ReadInt32ModuleOffset(string moduleName, string offsetHex)
    {
        var bytes = ReadModuleOffset(moduleName, offsetHex, 4);
        return bytes.Length < 4 ? null : BitConverter.ToInt32(bytes, 0);
    }

    public static byte[] ParseHexString(string hex)
    {
        var parts = hex.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = parts[i] == "??" || parts[i] == "?" ? (byte)0
                : byte.Parse(parts[i], System.Globalization.NumberStyles.HexNumber);
        return result;
    }

    public IntPtr InjectDllGetHandle(string dllPath)
    {
        if (_handle == IntPtr.Zero) return IntPtr.Zero;
        EnableDebugPrivilege();
        if (!System.IO.File.Exists(dllPath)) { LastErrorCode = -2; return IntPtr.Zero; }

        var bytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
        var alloc = Api.VirtualAllocEx(_handle, IntPtr.Zero, bytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (alloc == IntPtr.Zero) { LastErrorCode = Api.GetLastError(); return IntPtr.Zero; }
        if (!Api.WriteProcessMemory(_handle, alloc, bytes, bytes.Length, out _))
        { LastErrorCode = Api.GetLastError(); return IntPtr.Zero; }

        var loadLib = Api.AddressOfLoadLibraryW;
        IntPtr thread; int ret;

        thread = Api.CreateRemoteThread(_handle, IntPtr.Zero, 0, loadLib, alloc, 0, out _);
        if (thread != IntPtr.Zero)
        {
            Api.WaitForSingleObject(thread, 10000);
            Api.GetExitCodeThread(thread, out ret);
            Api.CloseHandle(thread);
            if (ret != 0) return new IntPtr(ret);
        }

        ret = Api.RtlCreateUserThread(_handle, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, loadLib, alloc, out thread, out _);
        if (ret == 0 && thread != IntPtr.Zero)
        {
            Api.WaitForSingleObject(thread, 10000);
            Api.GetExitCodeThread(thread, out ret);
            Api.CloseHandle(thread);
            if (ret != 0) return new IntPtr(ret);
        }

        LastErrorCode = -3;
        return IntPtr.Zero;
    }

    public bool FreeDll(IntPtr moduleHandle)
    {
        if (_handle == IntPtr.Zero || moduleHandle == IntPtr.Zero) return false;
        EnableDebugPrivilege();

        var feAddr = Api.AddressOfFreeLibraryAndExitThread;
        if (feAddr == IntPtr.Zero) { LastErrorCode = -1; return false; }

        byte[] sc = new byte[14];
        sc[0] = 0x6A; sc[1] = 0x00;
        sc[2] = 0x68;
        BitConverter.GetBytes(moduleHandle.ToInt32()).CopyTo(sc, 3);
        sc[7] = 0xB8;
        BitConverter.GetBytes(feAddr.ToInt32()).CopyTo(sc, 8);
        sc[12] = 0xFF; sc[13] = 0xD0;

        var alloc = Api.VirtualAllocEx(_handle, IntPtr.Zero, sc.Length, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (alloc == IntPtr.Zero) { LastErrorCode = Api.GetLastError(); return false; }
        if (!Api.WriteProcessMemory(_handle, alloc, sc, sc.Length, out _))
        { LastErrorCode = Api.GetLastError(); Api.VirtualFreeEx(_handle, alloc, 0, MEM_RELEASE); return false; }

        IntPtr thread;
        thread = Api.CreateRemoteThread(_handle, IntPtr.Zero, 0, alloc, IntPtr.Zero, 0, out _);
        if (thread != IntPtr.Zero)
        {
            Api.WaitForSingleObject(thread, 10000);
            Api.CloseHandle(thread);
            Api.VirtualFreeEx(_handle, alloc, 0, MEM_RELEASE);
            return true;
        }

        var rt = Api.RtlCreateUserThread(_handle, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, alloc, IntPtr.Zero, out thread, out _);
        if (rt == 0 && thread != IntPtr.Zero)
        {
            Api.WaitForSingleObject(thread, 10000);
            Api.CloseHandle(thread);
            Api.VirtualFreeEx(_handle, alloc, 0, MEM_RELEASE);
            return true;
        }

        Api.VirtualFreeEx(_handle, alloc, 0, MEM_RELEASE);
        LastErrorCode = -4;
        return false;
    }

    public bool InjectDll(string dllPath)
    {
        if (_handle == IntPtr.Zero) return false;
        EnableDebugPrivilege();
        if (!System.IO.File.Exists(dllPath)) { LastErrorCode = -2; return false; }

        var bytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
        var alloc = Api.VirtualAllocEx(_handle, IntPtr.Zero, bytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (alloc == IntPtr.Zero) { LastErrorCode = Api.GetLastError(); return false; }
        if (!Api.WriteProcessMemory(_handle, alloc, bytes, bytes.Length, out _))
        { LastErrorCode = Api.GetLastError(); return false; }

        return TryInject(Api.AddressOfLoadLibraryW, alloc);
    }

    private bool TryInject(IntPtr start, IntPtr param)
    {
        var t = Api.CreateRemoteThread(_handle, IntPtr.Zero, 0, start, param, 0, out _);
        if (t != IntPtr.Zero) { Api.CloseHandle(t); return true; }
        var r = Api.RtlCreateUserThread(_handle, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, start, param, out t, out _);
        if (r == 0 && t != IntPtr.Zero) { Api.CloseHandle(t); return true; }
        return false;
    }

    private void EnableDebugPrivilege()
    {
        try
        {
            Api.OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x0020, out var token);
            Api.LookupPrivilegeValue(null, "SeDebugPrivilege", out var luid);
            var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = 0x00000002 };
            Api.AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    public void Dispose() => Disconnect();
}