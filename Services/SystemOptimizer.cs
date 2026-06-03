using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace PointBlankPanel.Services;

public static class SystemOptimizer
{
    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, uint pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

    private const uint SPI_SETMOUSE = 0x0004;
    private const uint SPI_SETMOUSESPEED = 0x0071;
    private const uint SPI_SETKEYBOARDDELAY = 0x0017;
    private const uint SPI_SETKEYBOARDSPEED = 0x000B;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    private static readonly uint SPIF = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;

    private static readonly Dictionary<string, object?> _saved = [];

    private static void Save(string id, object? val)
    {
        if (!_saved.ContainsKey(id)) _saved[id] = val;
    }

    private static string SetDword(RegistryKey root, string path, string name, object val)
    {
        try
        {
            using var key = root.OpenSubKey(path, true);
            if (key == null) return "✘ Chave não encontrada: " + path;
            Save(path + "\\" + name, key.GetValue(name));
            key.SetValue(name, val, RegistryValueKind.DWord);
            return "✔";
        }
        catch (Exception ex) { return "✘ " + ex.Message; }
    }

    private static string SetString(RegistryKey root, string path, string name, string val)
    {
        try
        {
            using var key = root.OpenSubKey(path, true) ?? root.CreateSubKey(path);
            if (key == null) return "✘ Chave não encontrada ou não foi possível criar: " + path;
            Save(path + "\\" + name, key.GetValue(name));
            key.SetValue(name, val);
            return "✔";
        }
        catch (Exception ex) { return "✘ " + ex.Message; }
    }

    private static string GetSavedString(string id, string fallback = "")
    {
        if (_saved.TryGetValue(id, out var v) && v is string s) return s;
        return fallback;
    }

    private static int GetSavedInt(string id, int fallback = 0)
    {
        if (_saved.TryGetValue(id, out var v))
        {
            if (v is int i) return i;
            if (v is uint u) return (int)u;
        }
        return fallback;
    }

    private static RegistryKey? OpenLM(string path, bool writable = false)
    {
        try
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(path, writable);
        }
        catch { return null; }
    }

    private static string SetDword64(string path, string name, object val)
    {
        try
        {
            var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var key = hive.OpenSubKey(path, true) ?? hive.CreateSubKey(path);
            if (key == null) return "✘ Chave não encontrada ou não foi possível criar: " + path;
            using (key)
            {
                Save("HKLM\\" + path + "\\" + name, key.GetValue(name));
                int dword = val is uint u ? unchecked((int)u) : Convert.ToInt32(val);
                key.SetValue(name, dword, RegistryValueKind.DWord);
            }
            return "✔";
        }
        catch (Exception ex) { return "✘ " + ex.Message; }
    }

    private static string PowerCfg(string args)
    {
        return ExecuteProcess("powercfg.exe", args);
    }

    private static void SaveReg(string path, string name, object? val)
    {
        Save("HKLM\\" + path + "\\" + name, val);
    }

    private static int RestoreInt64(string path, string name, int fallback)
    {
        if (_saved.TryGetValue("HKLM\\" + path + "\\" + name, out var v))
        {
            if (v is int i) return i;
            if (v is uint u) return (int)u;
        }
        return fallback;
    }

    private static string ExecuteProcess(string file, string args)
    {
        try
        {
            var p = new System.Diagnostics.ProcessStartInfo(file, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = System.Diagnostics.Process.Start(p);
            proc?.WaitForExit(5000);
            return "✔";
        }
        catch (Exception ex) { return "✘ " + ex.Message; }
    }

    // ── Mouse ──

    public static string DesativarAceleracaoMouse()
    {
        var cu = Registry.CurrentUser;
        var r1 = SetString(cu, @"Control Panel\Mouse", "MouseSpeed", "0");
        var r2 = SetString(cu, @"Control Panel\Mouse", "MouseThreshold1", "0");
        var r3 = SetString(cu, @"Control Panel\Mouse", "MouseThreshold2", "0");
        if (r1[0] == '✘') return r1;
        if (r2[0] == '✘') return r2;
        if (r3[0] == '✘') return r3;
        SystemParametersInfo(SPI_SETMOUSE, 0, new int[] { 0, 0, 0 }, SPIF);
        return "✔ Aceleração do mouse desativada!";
    }

    public static string RestaurarAceleracaoMouse()
    {
        var cu = Registry.CurrentUser;
        SetString(cu, @"Control Panel\Mouse", "MouseSpeed", GetSavedString(@"Control Panel\Mouse\MouseSpeed", "1"));
        SetString(cu, @"Control Panel\Mouse", "MouseThreshold1", GetSavedString(@"Control Panel\Mouse\MouseThreshold1", "6"));
        SetString(cu, @"Control Panel\Mouse", "MouseThreshold2", GetSavedString(@"Control Panel\Mouse\MouseThreshold2", "10"));
        SystemParametersInfo(SPI_SETMOUSE, 0, new int[] {
            int.TryParse(GetSavedString(@"Control Panel\Mouse\MouseSpeed", "1"), out var ms) ? ms : 1,
            int.TryParse(GetSavedString(@"Control Panel\Mouse\MouseThreshold1", "6"), out var mt1) ? mt1 : 6,
            int.TryParse(GetSavedString(@"Control Panel\Mouse\MouseThreshold2", "10"), out var mt2) ? mt2 : 10,
        }, SPIF);
        return "✘ Aceleração do mouse restaurada.";
    }

    public static string VelocidadeMouseMaxima()
    {
        var cu = Registry.CurrentUser;
        var r = SetString(cu, @"Control Panel\Mouse", "MouseSensitivity", "6");
        if (r[0] == '✘') return r;
        SystemParametersInfo(SPI_SETMOUSESPEED, 0, 6, SPIF);
        return "✔ Sensibilidade ideal para FPS! (6/20)";
    }

    public static string RestaurarVelocidadeMouse()
    {
        SetString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSensitivity",
            GetSavedString(@"Control Panel\Mouse\MouseSensitivity", "10"));
        SystemParametersInfo(SPI_SETMOUSESPEED, 0,
            uint.TryParse(GetSavedString(@"Control Panel\Mouse\MouseSensitivity", "10"), out var sens) ? sens : 10u, SPIF);
        return "✘ Sensibilidade do mouse restaurada.";
    }

    public static string TecladoMaisRapido(int speed = 31, int delay = 0)
    {
        var cu = Registry.CurrentUser;
        var s = speed.ToString();
        var d = delay.ToString();
        var r1 = SetString(cu, @"Control Panel\Keyboard", "KeyboardSpeed", s);
        var r2 = SetString(cu, @"Control Panel\Keyboard", "KeyboardDelay", d);
        if (r1[0] == '✘') return r1;
        if (r2[0] == '✘') return r2;
        SystemParametersInfo(SPI_SETKEYBOARDSPEED, (uint)speed, 0, SPIF);
        SystemParametersInfo(SPI_SETKEYBOARDDELAY, (uint)delay, 0, SPIF);
        return $"✔ Teclado configurado: Speed={speed}/31, Delay={delay}/3";
    }

    public static string TecladoMaisRapido()
    {
        return TecladoMaisRapido(31, 0);
    }

    public static string RestaurarTeclado()
    {
        var cu = Registry.CurrentUser;
        SetString(cu, @"Control Panel\Keyboard", "KeyboardSpeed",
            GetSavedString(@"Control Panel\Keyboard\KeyboardSpeed", "31"));
        SetString(cu, @"Control Panel\Keyboard", "KeyboardDelay",
            GetSavedString(@"Control Panel\Keyboard\KeyboardDelay", "1"));
        SystemParametersInfo(SPI_SETKEYBOARDSPEED,
            uint.TryParse(GetSavedString(@"Control Panel\Keyboard\KeyboardSpeed", "31"), out var ks) ? ks : 31u, 0, SPIF);
        SystemParametersInfo(SPI_SETKEYBOARDDELAY,
            uint.TryParse(GetSavedString(@"Control Panel\Keyboard\KeyboardDelay", "1"), out var kd) ? kd : 1u, 0, SPIF);
        return "✘ Teclado restaurado.";
    }

    // ── CPU / Sistema ──

    public static string PrioridadeJogos()
    {
        var lm = Registry.LocalMachine;
        // Win32PrioritySeparation: 0x26 (38) = foreground boost + short quantum
        var r = SetDword(lm, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38);
        if (r[0] == '✘') return r;
        return "✔ Prioridade para jogos ativada (CPU)!";
    }

    public static string RestaurarPrioridadeJogos()
    {
        SetDword(Registry.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation",
            GetSavedInt(@"SYSTEM\CurrentControlSet\Control\PriorityControl\Win32PrioritySeparation", 2));
        return "✘ Prioridade CPU restaurada.";
    }

    public static string MaximoDesempenho()
    {
        var path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        var r1 = SetDword64(path, "SystemResponsiveness", 0);
        var r2 = SetDword64(path, "NetworkThrottlingIndex", 0xFFFFFFFF);
        if (r1[0] == '✘') return r1;
        if (r2[0] == '✘') return r2;
        return "✔ Máximo desempenho do sistema ativado!";
    }

    public static string RestaurarMaximoDesempenho()
    {
        var path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        SetDword64(path, "SystemResponsiveness", RestoreInt64(path, "SystemResponsiveness", 10));
        SetDword64(path, "NetworkThrottlingIndex", RestoreInt64(path, "NetworkThrottlingIndex", 10));
        return "✘ Desempenho do sistema restaurado.";
    }

    // ── Rede ──

    public static string MenorLatenciaRede()
    {
        var lm = Registry.LocalMachine;
        var path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        int ok = 0, fail = 0;
        try
        {
            using var interfaces = lm.OpenSubKey(path);
            if (interfaces == null) return "✘ Chave de interfaces não encontrada.";
            foreach (var guid in interfaces.GetSubKeyNames())
            {
                try
                {
                    using var iface = lm.OpenSubKey(path + "\\" + guid, true);
                    if (iface == null) continue;
                    Save(path + "\\" + guid + "\\TcpAckFrequency", iface.GetValue("TcpAckFrequency"));
                    iface.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                    ok++;
                }
                catch { fail++; }
            }
        }
        catch { return "✘ Erro ao acessar interfaces de rede."; }

        var r = SetDword(lm, @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "GlobalMaxTcpWindowSize", 65535);
        var r2 = SetDword(lm, @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TCPNoDelay", 1);
        if (r[0] == '✘') fail++;
        if (r2[0] == '✘') fail++;

        return $"✔ Latência de rede otimizada! {ok} interfaces configuradas.";
    }

    public static string RestaurarMenorLatenciaRede()
    {
        var lm = Registry.LocalMachine;
        var path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        try
        {
            using var interfaces = lm.OpenSubKey(path);
            if (interfaces != null)
            {
                foreach (var guid in interfaces.GetSubKeyNames())
                {
                    try
                    {
                        using var iface = lm.OpenSubKey(path + "\\" + guid, true);
                        if (iface == null) continue;
                        var saved = GetSavedInt(path + "\\" + guid + "\\TcpAckFrequency", 2);
                        if (saved != 0) iface.SetValue("TcpAckFrequency", saved, RegistryValueKind.DWord);
                    }
                    catch { }
                }
            }
        }
        catch { }

        SetDword(lm, @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "GlobalMaxTcpWindowSize",
            GetSavedInt(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\GlobalMaxTcpWindowSize", 65535));
        SetDword(lm, @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TCPNoDelay",
            GetSavedInt(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\TCPNoDelay", 0));
        return "✘ Latência de rede restaurada.";
    }

    // ── Energia ──

    public static string PlanoAltoDesempenho()
    {
        var r = ExecuteProcess("powercfg.exe", "/s 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        if (r[0] == '✘') return r;
        return "✔ Plano de alto desempenho ativado!";
    }

    public static string RestaurarPlanoEnergia()
    {
        ExecuteProcess("powercfg.exe", "/s 381b4222-f694-41f0-9685-ff5bb260df2e");
        return "✘ Plano de energia restaurado (Balanceado).";
    }

    public static string USBsemEconomia()
    {
        var usbGuid = "2a737441-1930-4402-8d77-b2bebba308a3";
        var usbSelGuid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";
        var r1 = PowerCfg("/setacvalueindex SCHEME_CURRENT " + usbGuid + " " + usbSelGuid + " 0");
        var r2 = PowerCfg("/setdcvalueindex SCHEME_CURRENT " + usbGuid + " " + usbSelGuid + " 0");
        if (r1[0] == '✘' && r2[0] == '✘') return r1;
        PowerCfg("/setactive SCHEME_CURRENT");
        return "✔ USB sem economia de energia!";
    }

    public static string RestaurarUSBsemEconomia()
    {
        var usbGuid = "2a737441-1930-4402-8d77-b2bebba308a3";
        var usbSelGuid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";
        PowerCfg("/setacvalueindex SCHEME_CURRENT " + usbGuid + " " + usbSelGuid + " 1");
        PowerCfg("/setdcvalueindex SCHEME_CURRENT " + usbGuid + " " + usbSelGuid + " 1");
        PowerCfg("/setactive SCHEME_CURRENT");
        return "✘ USB economia restaurada.";
    }

    public static string PCIexpressemEconomia()
    {
        var pciGuid = "501a4d13-42af-4429-9fd1-a8218c268e20";
        var aspmGuid = "ee12f906-dff1-4094-b096-8dbe9692bd8c";
        var r1 = PowerCfg("/setacvalueindex SCHEME_CURRENT " + pciGuid + " " + aspmGuid + " 0");
        var r2 = PowerCfg("/setdcvalueindex SCHEME_CURRENT " + pciGuid + " " + aspmGuid + " 0");
        if (r1[0] == '✘' && r2[0] == '✘') return r1;
        PowerCfg("/setactive SCHEME_CURRENT");
        return "✔ PCI Express sem economia de energia!";
    }

    public static string RestaurarPCIexpressemEconomia()
    {
        var pciGuid = "501a4d13-42af-4429-9fd1-a8218c268e20";
        var aspmGuid = "ee12f906-dff1-4094-b096-8dbe9692bd8c";
        PowerCfg("/setacvalueindex SCHEME_CURRENT " + pciGuid + " " + aspmGuid + " 2");
        PowerCfg("/setdcvalueindex SCHEME_CURRENT " + pciGuid + " " + aspmGuid + " 2");
        PowerCfg("/setactive SCHEME_CURRENT");
        return "✘ PCI Express economia restaurada.";
    }

    // ── GPU ──

    public static string PrevenirTimeoutGPU()
    {
        var lm = Registry.LocalMachine;
        var r1 = SetDword(lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay", 8);
        var r2 = SetDword(lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDdiDelay", 8);
        if (r1[0] == '✘') return r1;
        if (r2[0] == '✘') return r2;
        return "✔ Timeout da GPU aumentado para 8s!";
    }

    public static string RestaurarTimeoutGPU()
    {
        var path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        SetDword(Registry.LocalMachine, path, "TdrDelay",
            GetSavedInt(path + "\\TdrDelay", 2));
        SetDword(Registry.LocalMachine, path, "TdrDdiDelay",
            GetSavedInt(path + "\\TdrDdiDelay", 5));
        return "✘ Timeout da GPU restaurado.";
    }

    public static string GpuTweak()
    {
        int ok = 0, fail = 0;
        // Desativa otimização de energia NVIDIA (se existir)
        var nvPath = @"SOFTWARE\NVIDIA Corporation\Global\NVTweak\Devices";
        try
        {
            using var nv = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(nvPath, true);
            if (nv != null)
            {
                foreach (var sub in nv.GetSubKeyNames())
                {
                    try
                    {
                        using var dev = nv.OpenSubKey(sub, true);
                        if (dev == null) continue;
                        Save("HKLM\\" + nvPath + "\\" + sub + "\\PowerMizerEnable", dev.GetValue("PowerMizerEnable"));
                        dev.SetValue("PowerMizerEnable", 0, RegistryValueKind.DWord);
                        Save("HKLM\\" + nvPath + "\\" + sub + "\\PowerMizerPreferPerformance", dev.GetValue("PowerMizerPreferPerformance"));
                        dev.SetValue("PowerMizerPreferPerformance", 1, RegistryValueKind.DWord);
                        ok++;
                    }
                    catch { fail++; }
                }
            }
        }
        catch { }
        // Desativa otimização de energia da GPU via gráficos do Windows
        var lm = Registry.LocalMachine;
        var r1 = SetDword(lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "DisableEnergyEfficiency", 1);
        if (r1[0] == '✔') ok++; else fail++;
        if (ok == 0 && fail > 0) return "✘ GPU Tweak: nenhuma configuração encontrada.";
        return $"✔ GPU Tweak: {ok} otimizações aplicadas!";
    }

    public static string RestaurarGpuTweak()
    {
        var nvPath = @"SOFTWARE\NVIDIA Corporation\Global\NVTweak\Devices";
        try
        {
            using var nv = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(nvPath, true);
            if (nv != null)
            {
                foreach (var sub in nv.GetSubKeyNames())
                {
                    try
                    {
                        using var dev = nv.OpenSubKey(sub, true);
                        if (dev == null) continue;
                        var saved = RestoreInt64(nvPath + "\\" + sub, "PowerMizerEnable", 1);
                        dev.SetValue("PowerMizerEnable", saved, RegistryValueKind.DWord);
                        saved = RestoreInt64(nvPath + "\\" + sub, "PowerMizerPreferPerformance", 0);
                        dev.SetValue("PowerMizerPreferPerformance", saved, RegistryValueKind.DWord);
                    }
                    catch { }
                }
            }
        }
        catch { }
        SetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "DisableEnergyEfficiency",
            GetSavedInt(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\DisableEnergyEfficiency", 0));
        return "✘ GPU Tweak restaurado.";
    }

    public static string CpuTweak()
    {
        var path = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583";
        var r1 = SetDword64(path, "ValueMin", 0);
        var r2 = SetDword64(path, "ValueMax", 0);
        if (r1[0] == '✘' && r2[0] == '✘') return "✘ CPU Tweak: chave de energia não encontrada.";
        var r3 = PowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100");
        var r4 = PowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100");
        PowerCfg("/setactive SCHEME_CURRENT");
        return "✔ CPU Tweak: Core Parking desativado + energia máxima!";
    }

    public static string RestaurarCpuTweak()
    {
        var path = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583";
        SetDword64(path, "ValueMin", RestoreInt64(path, "ValueMin", 0));
        SetDword64(path, "ValueMax", RestoreInt64(path, "ValueMax", 100));
        PowerCfg("/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES " + GetSavedInt(path + "_CPMINCORES", 0));
        PowerCfg("/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES " + GetSavedInt(path + "_CPMINCORES", 0));
        PowerCfg("/setactive SCHEME_CURRENT");
        return "✘ CPU Tweak restaurado.";
    }

    public static string BloquearTeclaWindows()
    {
        var cu = Registry.CurrentUser;
        var policies = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        var r = SetDword(cu, policies, "NoWinKeys", 1);
        if (r[0] == '✘') return r;
        return "✔ Tecla Windows bloqueada durante o jogo!";
    }

    public static string RestaurarBloquearTeclaWindows()
    {
        var cu = Registry.CurrentUser;
        var policies = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        SetDword(cu, policies, "NoWinKeys",
            GetSavedInt(policies + "\\NoWinKeys", 0));
        return "✘ Tecla Windows liberada.";
    }

    // ── Sistema / Visual ──

    public static string DesativarAnimacoes()
    {
        var cu = Registry.CurrentUser;
        var path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        var r = SetDword(cu, path, "VisualFXSetting", 2);
        if (r[0] == '✘') return r;
        return "✔ Animações do Windows desativadas!";
    }

    public static string RestaurarAnimacoes()
    {
        var path = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        SetDword(Registry.CurrentUser, path, "VisualFXSetting",
            GetSavedInt(path + "\\VisualFXSetting", 3));
        return "✘ Animações restauradas.";
    }

    public static string DesativarGameBar()
    {
        var cu = Registry.CurrentUser;
        var r1 = SetDword(cu, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
        var r2 = SetDword(cu, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 0);
        var r3 = SetDword(cu, @"Software\Microsoft\GameBar", "ShowStartupPanel", 0);

        var policyPath = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .CreateSubKey(policyPath);
            if (key != null)
            {
                Save("HKLM\\" + policyPath + "\\AllowGameDVR", key.GetValue("AllowGameDVR"));
                key.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
            }
        }
        catch { }

        if (r1[0] == '✘') return r1;
        return "✔ Xbox Game Bar / DVR desativados!";
    }

    public static string RestaurarGameBar()
    {
        var cu = Registry.CurrentUser;
        var path1 = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        SetDword(cu, path1, "AppCaptureEnabled",
            GetSavedInt(path1 + "\\AppCaptureEnabled", 1));
        var path2 = @"Software\Microsoft\GameBar";
        SetDword(cu, path2, "AllowAutoGameMode",
            GetSavedInt(path2 + "\\AllowAutoGameMode", 1));
        SetDword(cu, path2, "ShowStartupPanel",
            GetSavedInt(path2 + "\\ShowStartupPanel", 1));

        var policyPath = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(policyPath, true);
            if (key != null)
            {
                var saved = RestoreInt64(policyPath, "AllowGameDVR", 1);
                key.SetValue("AllowGameDVR", saved, RegistryValueKind.DWord);
            }
        }
        catch { }
        return "✘ Game Bar restaurado.";
    }

    public static string DesativarAcessibilidade()
    {
        var cu = Registry.CurrentUser;
        var path = @"Control Panel\Accessibility\StickyKeys";
        var r1 = SetString(cu, path, "Flags", "506");
        path = @"Control Panel\Accessibility\FilterKeys";
        var r2 = SetString(cu, path, "Flags", "122");
        path = @"Control Panel\Accessibility\ToggleKeys";
        var r3 = SetString(cu, path, "Flags", "58");
        if (r1[0] == '✘') return r1;
        if (r2[0] == '✘') return r2;
        if (r3[0] == '✘') return r3;
        return "✔ Sticky/Filter/Toggle Keys desativadas!";
    }

    public static string RestaurarAcessibilidade()
    {
        var cu = Registry.CurrentUser;
        SetString(cu, @"Control Panel\Accessibility\StickyKeys", "Flags",
            GetSavedString(@"Control Panel\Accessibility\StickyKeys\Flags", "510"));
        SetString(cu, @"Control Panel\Accessibility\FilterKeys", "Flags",
            GetSavedString(@"Control Panel\Accessibility\FilterKeys\Flags", "126"));
        SetString(cu, @"Control Panel\Accessibility\ToggleKeys", "Flags",
            GetSavedString(@"Control Panel\Accessibility\ToggleKeys\Flags", "62"));
        return "✘ Acessibilidade restaurada.";
    }

    // ── Serviços ──

    private static readonly string[] _services = ["SysMain", "WSearch", "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "DiagTrack", "dmwappushservice"];

    public static string DesativarServicos()
    {
        int ok = 0, fail = 0;
        foreach (var name in _services)
        {
            try
            {
                using var sc = new ServiceController(name);
                Save("SERVICE_START_" + name, (int)sc.StartType);
                if (sc.CanStop && sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                }
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, true);
                if (key != null)
                {
                    Save("SERVICE_REG_" + name, key.GetValue("Start"));
                    key.SetValue("Start", 4, RegistryValueKind.DWord);
                }
                ok++;
            }
            catch { fail++; }
        }
        return $"✔ Serviços: {ok} desativados, {fail} falha(s)";
    }

    public static string RestaurarServicos()
    {
        foreach (var name in _services)
        {
            try
            {
                var savedStart = GetSavedInt("SERVICE_REG_" + name, 3);
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, true);
                if (key != null) key.SetValue("Start", savedStart, RegistryValueKind.DWord);
                var startType = GetSavedInt("SERVICE_START_" + name, 3);
                if (startType != 4)
                {
                    using var sc = new ServiceController(name);
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    }
                }
            }
            catch { }
        }
        return "✘ Serviços restaurados.";
    }

    // ── Aplicar / Restaurar por nome ──

    public static string Apply(string name)
    {
        return name switch
        {
            "Desativar Aceleração Mouse" => DesativarAceleracaoMouse(),
            "Sensibilidade Máxima Mouse" => VelocidadeMouseMaxima(),
            "Teclado Mais Rápido" => TecladoMaisRapido(),
            "Prioridade para Jogos" => PrioridadeJogos(),
            "Máximo Desempenho Sistema" => MaximoDesempenho(),
            "Menor Latência de Rede" => MenorLatenciaRede(),
            "Plano Alto Desempenho" => PlanoAltoDesempenho(),
            "USB sem Economia" => USBsemEconomia(),
            "PCI Express sem Economia" => PCIexpressemEconomia(),
            "Prevenir Timeout GPU" => PrevenirTimeoutGPU(),
            "Desativar Animações" => DesativarAnimacoes(),
            "Desativar Xbox Game Bar" => DesativarGameBar(),
            "Desativar Teclas Acessibilidade" => DesativarAcessibilidade(),
            "GPU Tweak" => GpuTweak(),
            "CPU Tweak" => CpuTweak(),
            "Bloquear Tecla Windows" => BloquearTeclaWindows(),
            "Desativar Serviços Windows" => DesativarServicos(),
            _ => "✘ Tweak desconhecido.",
        };
    }

    public static string Restore(string name)
    {
        return name switch
        {
            "Desativar Aceleração Mouse" => RestaurarAceleracaoMouse(),
            "Sensibilidade Máxima Mouse" => RestaurarVelocidadeMouse(),
            "Teclado Mais Rápido" => RestaurarTeclado(),
            "Prioridade para Jogos" => RestaurarPrioridadeJogos(),
            "Máximo Desempenho Sistema" => RestaurarMaximoDesempenho(),
            "Menor Latência de Rede" => RestaurarMenorLatenciaRede(),
            "Plano Alto Desempenho" => RestaurarPlanoEnergia(),
            "USB sem Economia" => RestaurarUSBsemEconomia(),
            "PCI Express sem Economia" => RestaurarPCIexpressemEconomia(),
            "Prevenir Timeout GPU" => RestaurarTimeoutGPU(),
            "Desativar Animações" => RestaurarAnimacoes(),
            "Desativar Xbox Game Bar" => RestaurarGameBar(),
            "Desativar Teclas Acessibilidade" => RestaurarAcessibilidade(),
            "GPU Tweak" => RestaurarGpuTweak(),
            "CPU Tweak" => RestaurarCpuTweak(),
            "Bloquear Tecla Windows" => RestaurarBloquearTeclaWindows(),
            "Desativar Serviços Windows" => RestaurarServicos(),
            _ => "✘ Tweak desconhecido.",
        };
    }
}
