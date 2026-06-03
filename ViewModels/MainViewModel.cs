using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using PointBlankPanel.Models;
using PointBlankPanel.Services;

namespace PointBlankPanel.ViewModels;

public class SidebarItem : INotifyPropertyChanged
{
    public string Icon { get; set; } = "";
    public string Label { get; set; } = "";
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly MemoryService _memory = new();
    private string _statusMessage = "Pronto";
    private bool _isConnected;
    private IntPtr _chamsModuleHandle = IntPtr.Zero;
    private string _searchText = "";
    private readonly DispatcherTimer _islcTimer = new();
    private readonly DispatcherTimer _ramTimer = new();
    private readonly DispatcherTimer _autoAcceptTimer = new();
    private readonly DispatcherTimer _antiAfkTimer = new();
    private readonly DispatcherTimer _autoHealthTimer = new();
    private string _ramInfo = "";
    private string _dashboardInfo = "";
    private bool _showLog;
    private int _keyboardSpeed = 31;
    private int _keyboardDelay = 0;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int w, int h, uint flags);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out IntPtr phkResult);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegSetValueEx(IntPtr hKey, string lpValueName, uint Reserved, uint dwType, byte[] lpData, int cbData);
    [DllImport("advapi32.dll")]
    private static extern int RegDeleteValue(IntPtr hKey, string lpValueName);
    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr hKey);
    private static readonly IntPtr HKEY_CURRENT_USER = new(unchecked((int)0x80000001));
    private const int KEY_SET_VALUE = 0x0002;
    private const int KEY_QUERY_VALUE = 0x0001;
    private const uint REG_SZ = 1;

    public ObservableCollection<TweakFunction> Tweaks { get; } = [];
    public ObservableCollection<SidebarItem> Categories { get; } = [];

    public List<TweakFunction> CurrentItems
    {
        get
        {
            var cat = Categories.FirstOrDefault(c => c.IsSelected);
            if (cat == null) return [];
            return Tweaks.Where(t => t.Category == cat.Label && Matches(t)).ToList();
        }
    }

    private string _selectedCategory = "PLAYER";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentItems));
            OnPropertyChanged(nameof(SearchPlaceholder));
        }
    }

    public string SearchPlaceholder => $"🔍  Buscar em {SelectedCategory}...";

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    public string RamInfo
    {
        get => _ramInfo;
        set { _ramInfo = value; OnPropertyChanged(); }
    }

    public string DashboardInfo
    {
        get => _dashboardInfo;
        set { _dashboardInfo = value; OnPropertyChanged(); }
    }

    public bool ShowLog
    {
        get => _showLog;
        set { _showLog = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> ActionLog { get; } = [];

    public int KeyboardSpeed
    {
        get => _keyboardSpeed;
        set { _keyboardSpeed = value; OnPropertyChanged(); }
    }

    public int KeyboardDelay
    {
        get => _keyboardDelay;
        set { _keyboardDelay = value; OnPropertyChanged(); }
    }

    public ICommand ConnectCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand EnableAllCommand { get; }
    public ICommand DisableAllCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand OtimizarTudoCommand { get; }
    public ICommand RestaurarPadroesCommand { get; }
    public ICommand SelectAccentCommand { get; }
    public ICommand ToggleLogCommand { get; }
    public ICommand CheckUpdateCommand { get; }

    public static ThemeColor[] Themes { get; } =
    {
        new("Roxo",    Color.FromRgb(0xB0,0x26,0xFF), Color.FromRgb(0xD4,0x55,0xFF), Color.FromRgb(0xB0,0x26,0xFF), Color.FromRgb(0xFF,0x00,0xFF), Color.FromRgb(0x66,0x00,0xCC), Color.FromRgb(0x00,0xE5,0xFF)),
        new("Ciano",   Color.FromRgb(0x00,0xE5,0xFF), Color.FromRgb(0x66,0xF0,0xFF), Color.FromRgb(0x00,0xE5,0xFF), Color.FromRgb(0x00,0x88,0xFF), Color.FromRgb(0x00,0x66,0xCC), Color.FromRgb(0xB0,0x26,0xFF)),
        new("Verde",   Color.FromRgb(0x00,0xE6,0x76), Color.FromRgb(0x69,0xF0,0xAE), Color.FromRgb(0x00,0xE6,0x76), Color.FromRgb(0x00,0xB8,0x5C), Color.FromRgb(0x00,0x80,0x40), Color.FromRgb(0x00,0xE5,0xFF)),
        new("Vermelho",Color.FromRgb(0xFF,0x00,0x80), Color.FromRgb(0xFF,0x66,0xB3), Color.FromRgb(0xFF,0x00,0x80), Color.FromRgb(0xD4,0x00,0xFF), Color.FromRgb(0x99,0x00,0x66), Color.FromRgb(0x00,0xE5,0xFF)),
        new("Dourado", Color.FromRgb(0xFF,0xD7,0x00), Color.FromRgb(0xFF,0xE6,0x66), Color.FromRgb(0xFF,0xD7,0x00), Color.FromRgb(0xFF,0x8C,0x00), Color.FromRgb(0xCC,0x66,0x00), Color.FromRgb(0x00,0xE5,0xFF)),
        new("Branco",  Color.FromRgb(0xDD,0xDD,0xDD), Color.FromRgb(0xFF,0xFF,0xFF), Color.FromRgb(0xCC,0xCC,0xCC), Color.FromRgb(0x99,0x99,0x99), Color.FromRgb(0x77,0x77,0x77), Color.FromRgb(0x00,0xE5,0xFF)),
    };

    public MainViewModel()
    {
        ConnectCommand = new RelayCommand(_ => Connect());
        ToggleCommand = new RelayCommand(ToggleFunction);
        EnableAllCommand = new RelayCommand(_ => SetAll(true));
        DisableAllCommand = new RelayCommand(_ => SetAll(false));
        SelectCategoryCommand = new RelayCommand(SelectCategory);
        OtimizarTudoCommand = new RelayCommand(_ => OtimizarTudo());
        RestaurarPadroesCommand = new RelayCommand(_ => RestaurarPadroes());
        SelectAccentCommand = new RelayCommand(SelectAccent);
        ToggleLogCommand = new RelayCommand(_ => ShowLog = !ShowLog);
        CheckUpdateCommand = new RelayCommand(_ => _ = CheckUpdateAsync());

        _islcTimer.Interval = TimeSpan.FromSeconds(15);
        _islcTimer.Tick += (_, _) => MemoryService.CleanStandbyList();

        _ramTimer.Interval = TimeSpan.FromSeconds(2);
        _ramTimer.Tick += (_, _) => { UpdateRamInfo(); UpdateDashboard(); };

        _autoAcceptTimer.Interval = TimeSpan.FromSeconds(1);
        _autoAcceptTimer.Tick += (_, _) => AutoAcceptTick();

        _antiAfkTimer.Interval = TimeSpan.FromSeconds(30);
        _antiAfkTimer.Tick += (_, _) => AntiAfkTick();

        _autoHealthTimer.Interval = TimeSpan.FromSeconds(5);
        _autoHealthTimer.Tick += (_, _) => AutoHealthTick();

        LoadCategories();
        LoadTweaks();
        LoadSettings();
        _ = AutoConnectAsync();
        RefreshCurrent();
    }

    private void LoadCategories()
    {
        var items = new[]
        {
            ("⚔", "PLAYER"), ("👁", "VISUAL"), ("🖥", "SISTEMAS"),
            ("🚀", "BOOSTER"), ("🖱", "PERIFÉRICOS"),
            ("💾", "MEMÓRIA"), ("🔧", "UTILITÁRIOS"), ("⚙", "CONFIG"),
        };
        foreach (var (icon, label) in items)
            Categories.Add(new SidebarItem { Icon = icon, Label = label, IsSelected = label == "PLAYER" });
    }

    private void SelectCategory(object? param)
    {
        if (param is not string label) return;
        foreach (var c in Categories) c.IsSelected = c.Label == label;
        SelectedCategory = label;
        if (label == "MEMÓRIA") _ramTimer.Start(); else _ramTimer.Stop();
    }

    private void RefreshCurrent()
    {
        OnPropertyChanged(nameof(CurrentItems));
        OnPropertyChanged(nameof(SearchPlaceholder));
    }

    private async Task AutoConnectAsync()
    {
        var autoConnect = true;
        var settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        try
        {
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                if (dict != null && dict.TryGetValue("Auto-Conectar", out var ac)) autoConnect = ac;
            }
        }
        catch { }

        if (!autoConnect) return;
        await Task.Delay(800);
        Connect();
    }

    private void UpdateRamInfo()
    {
        var mse = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref mse)) return;
        double totalGB = mse.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
        double freeGB = mse.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
        double usedGB = totalGB - freeGB;
        RamInfo = $"💾 RAM: {usedGB:F1} GB / {totalGB:F1} GB usados ({mse.dwMemoryLoad}%)";
    }

    private void AutoAcceptTick()
    {
        var procs = System.Diagnostics.Process.GetProcessesByName("PointBlank");
        if (procs.Length == 0) procs = System.Diagnostics.Process.GetProcessesByName("PB");
        if (procs.Length == 0) return;
        try
        {
            var proc = procs[0];
            if (proc.MainWindowHandle == IntPtr.Zero) return;
            if (proc.MainWindowTitle.Contains("Match", StringComparison.OrdinalIgnoreCase) ||
                proc.MainWindowTitle.Contains("Partida", StringComparison.OrdinalIgnoreCase))
            {
                var hwnd = proc.MainWindowHandle;
                System.Windows.Forms.SendKeys.SendWait("{ENTER}");
            }
        }
        catch { }
    }

    private void AntiAfkTick()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcessesByName("PointBlank");
            if (procs.Length == 0) procs = System.Diagnostics.Process.GetProcessesByName("PB");
            if (procs.Length == 0) return;
            System.Windows.Forms.SendKeys.SendWait("{w}");
            System.Threading.Thread.Sleep(50);
            System.Windows.Forms.SendKeys.SendWait("{s}");
        }
        catch { }
    }

    private void AutoHealthTick()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcessesByName("PointBlank");
            if (procs.Length == 0) procs = System.Diagnostics.Process.GetProcessesByName("PB");
            if (procs.Length == 0) return;
            System.Windows.Forms.SendKeys.SendWait("{F1}");
        }
        catch { }
    }

    private void AddLog(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        ActionLog.Add($"[{time}] {message}");
        if (ActionLog.Count > 200) ActionLog.RemoveAt(0);
    }

    private void UpdateDashboard()
    {
        var parts = new System.Collections.Generic.List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            foreach (var obj in searcher.Get())
                parts.Add($"🖥 CPU: {obj["PercentProcessorTime"]}%");
        }
        catch { }
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith("C:"));
            if (drive != null)
            {
                double total = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                double free = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                parts.Add($"💿 C:\\: {total - free:F1}/{total:F1} GB");
            }
        }
        catch { }
        DashboardInfo = string.Join("  ·  ", parts);
    }

    private void LoadTweaks()
    {
        // PLAYER
        AddTweak("💥", "No Flash Bang", "Remove o efeito de flashbang", "PLAYER",
            "PointBlank.exe", "6B014E",
            "7A 07 32 C0 E9 C9 01 00 00 C7 46 28 FF FF FF",
            "90 90 32 C0 E9 C9 01 00 00 C7 46 28 FF FF FF");
        AddTweak("🛡", "Anti Kick", "Protege contra votos de kick", "PLAYER",
            "PointBlank.exe", "7F00A5",
            "78 15 0F 9F C0 0F B6 C0 50 E8 F4 DB FF FF 6A",
            "EB 15 0F 9F C0 0F B6 C0 50 E8 F4 DB FF FF 6A");
        AddTweak("🎯", "Precisão", "Aumenta a precisão das armas", "PLAYER",
            "PointBlank.exe", "69A079",
            "D9 5D FC F3 0F 10 45 FC 0F 2E 46 4C 9F F6 C4",
            "00 5D FC F3 0F 10 45 FC 0F 2E 46 4C 9F F6 C4");
        AddTweak("🪖", "No Helmet", "Remove capacete dos inimigos", "PLAYER",
            "PointBlank.exe", "466C33",
            "D9 5D FC F3 0F 10 45 FC 0F 2E 05 EC 30 10 01",
            "90 90 90 F3 0F 10 45 FC 0F 2E 05 EC 30 10 01");
        AddTweak("📈", "Recoil Só Sobe", "Remove recuo horizontal", "PLAYER",
            "PointBlank.exe", "6F7181",
            "D9 5D 08 0F 2F 45 08 76 11 E8 11 13 CB FF D8",
            "00 5D 08 0F 2F 45 08 76 11 E8 11 13 CB FF D8");
        AddTweak("↔", "Recoil Sem Lateral", "Remove movimento lateral do recuo", "PLAYER",
            "PointBlank.exe", "3A84CB",
            "D9 45 FC 8B E5 5D C3 CC CC CC CC CC CC CC CC",
            "D9 45 00 8B E5 5D C3 CC CC CC CC CC CC CC CC");

        // VISUAL
        AddTweak("🗺", "Map Hack", "Mostra todos os inimigos no mapa", "VISUAL",
            "PointBlank.exe", "9623C1",
            "55 8B EC A1 E0 26 59 01 83 EC 0C 53 56 57 8B 79 0C",
            "C3 8B EC A1 40 C3 45 01 83 EC 0C 53 56 57 8B");
        AddTweak("⏩", "Load Interface", "Remove delay do carregamento", "VISUAL",
            "PointBlank.exe", "D44A20",
            "00 00 80 00 00 00 80 3B 00 00",
            "00 3C 1C 46 00 00 80 3B 00 00");
        AddTweak("📂", "Load Map", "Remove delay do carregamento de mapa", "VISUAL",
            "PointBlank.exe", "DC605C",
            "A4 70 7D 3F 55 69 53 68 61 70",
            "00 3C 1C C6 55 69 53 68 61 70");
        AddTweak("👁", "ESP Name", "Mostra nome dos inimigos", "VISUAL",
            "PointBlank.exe", "6A03E0",
            "C6 01 00 8D 49 08 83 E8 01 75 F5 C3 A1 A0 92",
            "C6 01 01 8D 49 08 83 E8 01 75 F5 C3 A1 A0 92");
        Tweaks.Add(new TweakFunction { Icon = "🌈", Name = "Chams", Description = "Injetar DLL de Chams Wireframe", Category = "VISUAL", Module = "", Offset = "", OffBytes = "", OnBytes = "" });

        // BOOSTER
        Tweaks.Add(new TweakFunction { Icon = "⏱", Name = "Timer Resolution", Description = "Reduz latência do sistema para 0.5ms", Category = "BOOSTER", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "🧹", Name = "ISLC", Description = "Limpa lista de espera da memória RAM a cada 15s", Category = "BOOSTER", Module = "", Offset = "", OffBytes = "", OnBytes = "" });

        // SISTEMAS
        foreach (var tw in new string[][] {
            ["⚡", "Prioridade para Jogos", "Win32PrioritySeparation para melhor resposta em jogos"],
            ["📊", "Máximo Desempenho Sistema", "SystemResponsiveness=0 + desativa throttling de rede"],
            ["🌐", "Menor Latência de Rede", "TCPNoDelay + TcpAckFrequency=1 em todas interfaces"],
            ["🔋", "Plano Alto Desempenho", "Ativa plano de energia de alto desempenho"],
            ["🔌", "USB sem Economia", "Desativa suspensão seletiva de USB"],
            ["🔗", "PCI Express sem Economia", "Desativa economia de energia PCI Express"],
            ["🎮", "Prevenir Timeout GPU", "Aumenta TdrDelay/TdrDdiDelay para 8s (evita crash)"],
            ["❌", "Desativar Xbox Game Bar", "Desativa Game DVR e Game Bar"],
            ["✨", "Desativar Animações", "Desativa animações visuais do Windows"],
            ["♿", "Desativar Teclas Acessibilidade", "Desativa Sticky/Filter/Toggle Keys"],
            ["🖥", "GPU Tweak", "NVIDIA Power Management + otimizações de GPU para jogos"],
            ["🔧", "CPU Tweak", "Desativa Core Parking + prioridade máxima para processos em foreground"],
            ["🚫", "Desativar Serviços Windows", "SysMain, WSearch, Xbox, DiagTrack e mais"],
        }) Tweaks.Add(new TweakFunction { Icon = tw[0], Name = tw[1], Description = tw[2], Category = "SISTEMAS", Module = "", Offset = "", OffBytes = "", OnBytes = "" });

        // PERIFÉRICOS
        foreach (var tw in new string[][] {
            ["🖱", "Desativar Aceleração Mouse", "Remove aceleração do mouse (Enhance Pointer Precision)"],
            ["🎯", "Sensibilidade Máxima Mouse", "Sensibilidade do mouse ideal para FPS (6/20)"],
            ["⌨", "Teclado Mais Rápido", "Velocidade máxima de repetição e delay mínimo"],
            ["🔒", "Bloquear Tecla Windows", "Bloqueia a tecla Windows durante o jogo"],
        }) Tweaks.Add(new TweakFunction { Icon = tw[0], Name = tw[1], Description = tw[2], Category = "PERIFÉRICOS", Module = "", Offset = "", OffBytes = "", OnBytes = "" });

        // MEMÓRIA
        Tweaks.Add(new TweakFunction { Icon = "📊", Name = "Monitor de RAM", Description = "Exibe uso de memória RAM em tempo real", Category = "MEMÓRIA", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "🧽", Name = "Limpar Working Set", Description = "Limpa working set de todos os processos", Category = "MEMÓRIA", Module = "", Offset = "", OffBytes = "", OnBytes = "" });

        // UTILITÁRIOS
        Tweaks.Add(new TweakFunction { Icon = "📌", Name = "Sempre em Cima", Description = "Mantém a janela sempre visível acima de todas", Category = "UTILITÁRIOS", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "✅", Name = "Auto-Aceitar Partida", Description = "Aperta ENTER automaticamente na tela de match", Category = "UTILITÁRIOS", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "🔄", Name = "Reconectar Rápido", Description = "Reconecta ao processo PointBlank", Category = "UTILITÁRIOS", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "📊", Name = "Dashboard Sistema", Description = "Monitora CPU, RAM e Disco em tempo real", Category = "UTILITÁRIOS", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "💤", Name = "Anti AFK", Description = "Simula movimento a cada 30s para não ser kickado", Category = "VISUAL", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "❤", Name = "Auto-Health", Description = "Usa kit médico automático a cada 5s", Category = "VISUAL", Module = "", Offset = "", OffBytes = "", OnBytes = "" });

        // CONFIG
        Tweaks.Add(new TweakFunction { Icon = "🖥", Name = "Iniciar com Windows", Description = "Executa o app automaticamente na inicialização", Category = "CONFIG", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "🔗", Name = "Auto-Conectar", Description = "Conecta automaticamente ao PointBlank ao abrir", Category = "CONFIG", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "📋", Name = "Iniciar Minimizado", Description = "Inicia o app minimizado na bandeja do sistema", Category = "CONFIG", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
        Tweaks.Add(new TweakFunction { Icon = "🔄", Name = "Auto-Update", Description = "Verificar e baixar atualizações automaticamente", Category = "CONFIG", Module = "", Offset = "", OffBytes = "", OnBytes = "" });
    }

    private void AddTweak(string icon, string name, string desc, string category,
        string module, string offset, string offBytes, string onBytes)
        => Tweaks.Add(new TweakFunction { Icon = icon, Name = name, Description = desc, Category = category, Module = module, Offset = offset, OffBytes = offBytes, OnBytes = onBytes });

    private void Connect()
    {
        if (IsConnected) return;
        StatusMessage = "Conectando ao PointBlank...";
        if (_memory.ConnectAny("PointBlank", "pointblank", "PB", "pb"))
        {
            IsConnected = true;
            var baseAddr = _memory.GetModuleBaseAddress("PointBlank.exe");
            var addr = baseAddr != null ? $"0x{baseAddr.Value.ToInt64():X8}" : "ok";
            StatusMessage = baseAddr != null ? $"✔ Conectado! Base: 0x{baseAddr.Value.ToInt64():X8}" : "✔ Conectado";
            AddLog($"Conectado ao PointBlank (base: {addr})");
        }
        else StatusMessage = "✘ PointBlank não encontrado! Execute como Administrador.";
    }

    private void ToggleFunction(object? param)
    {
        if (param is not TweakFunction tweak) return;

        if (tweak.Name == "Chams")
        {
            if (!tweak.IsActive)
            {
                var dllPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Wireframe-Fulldot (2).dll");
                if (!System.IO.File.Exists(dllPath))
                    System.IO.File.Copy(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wireframe-Fulldot (2).dll"), dllPath, true);
                if (!System.IO.File.Exists(dllPath)) { StatusMessage = "✘ DLL não encontrada!"; return; }
                _chamsModuleHandle = _memory.InjectDllGetHandle(dllPath);
                if (_chamsModuleHandle != IntPtr.Zero) { RefreshCurrent(); StatusMessage = "✔ Chams injetado!"; }
                else { tweak.IsActive = false; StatusMessage = $"✘ Falha injeção! Erro: {_memory.LastErrorCode}"; }
            }
            else { tweak.IsActive = true; StatusMessage = "ℹ Chams fica ativo até reiniciar o jogo (DLL não pode ser descarregada)"; }
            SaveSettings(); return;
        }

        if (tweak.Name == "Timer Resolution")
        {
            if (tweak.IsActive) { var r = MemoryService.SetTimerResolution500us(); StatusMessage = r; if (r[0] != '✔') tweak.IsActive = false; AddLog(r); }
            else { StatusMessage = MemoryService.RestoreTimerResolution(); AddLog(StatusMessage); }
            RefreshCurrent(); SaveSettings(); return;
        }

        if (tweak.Name == "ISLC")
        {
            if (tweak.IsActive) { _islcTimer.Start(); MemoryService.CleanStandbyList(); StatusMessage = "✔ ISLC ativado! Limpando a cada 15s."; }
            else { _islcTimer.Stop(); StatusMessage = "✘ ISLC desativado."; }
            AddLog(StatusMessage); RefreshCurrent(); SaveSettings(); return;
        }

        if (tweak.Name == "Limpar Working Set")
        {
            tweak.IsActive = false;
            int count = 0;
            foreach (var proc in System.Diagnostics.Process.GetProcesses())
            { try { if (MemoryService.EmptyWorkingSetByPid(proc.Id)) count++; } catch { } }
            StatusMessage = $"✔ Working set limpo em {count} processos.";
            RefreshCurrent(); return;
        }

        if (tweak.Name == "Reconectar Rápido")
        {
            tweak.IsActive = false;
            if (IsConnected) { _memory.Disconnect(); IsConnected = false; }
            Connect();
            RefreshCurrent(); return;
        }

        if (tweak.Name == "Sempre em Cima")
        {
            var w = Application.Current.MainWindow;
            if (w == null) { tweak.IsActive = false; StatusMessage = "✘ Erro ao obter janela."; RefreshCurrent(); return; }
            var hWnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            if (tweak.IsActive)
            {
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                StatusMessage = "✔ Janela sempre no topo!";
            }
            else
            {
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                StatusMessage = "✘ Modo sempre no topo desativado.";
            }
            SaveSettings(); return;
        }

        if (tweak.Name == "Auto-Aceitar Partida")
        {
            if (tweak.IsActive) { _autoAcceptTimer.Start(); StatusMessage = "✔ Auto-Aceitar ativado! (verificando a cada 1s)"; }
            else { _autoAcceptTimer.Stop(); StatusMessage = "✘ Auto-Aceitar desativado."; }
            SaveSettings(); return;
        }

        if (tweak.Name == "Anti AFK")
        {
            if (tweak.IsActive) { _antiAfkTimer.Start(); StatusMessage = "✔ Anti AFK ativado! (movimento a cada 30s)"; }
            else { _antiAfkTimer.Stop(); StatusMessage = "✘ Anti AFK desativado."; }
            SaveSettings(); return;
        }

        if (tweak.Name == "Auto-Health")
        {
            if (tweak.IsActive) { _autoHealthTimer.Start(); StatusMessage = "✔ Auto-Health ativado! (kit médico a cada 5s)"; }
            else { _autoHealthTimer.Stop(); StatusMessage = "✘ Auto-Health desativado."; }
            SaveSettings(); return;
        }

        if (tweak.Name == "Monitor de RAM")
        {
            if (tweak.IsActive) { _ramTimer.Start(); UpdateRamInfo(); StatusMessage = "✔ Monitor de RAM ativado!"; }
            else { _ramTimer.Stop(); RamInfo = ""; StatusMessage = "✘ Monitor de RAM desativado."; }
            SaveSettings(); return;
        }

        if (tweak.Name == "Teclado Mais Rápido")
        {
            if (tweak.IsActive) { var r = SystemOptimizer.TecladoMaisRapido(KeyboardSpeed, KeyboardDelay); StatusMessage = r; if (r[0] != '✔') tweak.IsActive = false; }
            else StatusMessage = SystemOptimizer.RestaurarTeclado();
            RefreshCurrent(); SaveSettings(); return;
        }

        if (tweak.Name == "Dashboard Sistema")
        {
            if (tweak.IsActive) { _ramTimer.Start(); UpdateRamInfo(); UpdateDashboard(); StatusMessage = "✔ Dashboard ativado!"; }
            else { _ramTimer.Stop(); RamInfo = ""; DashboardInfo = ""; StatusMessage = "✘ Dashboard desativado."; }
            RefreshCurrent(); SaveSettings(); return;
        }

        if (tweak.Category is "SISTEMAS" or "PERIFÉRICOS")
        {
            if (tweak.IsActive) { var r = SystemOptimizer.Apply(tweak.Name); StatusMessage = r; if (r[0] != '✔') tweak.IsActive = false; AddLog(r); }
            else { StatusMessage = SystemOptimizer.Restore(tweak.Name); AddLog(StatusMessage); }
            RefreshCurrent(); SaveSettings(); return;
        }

        // CONFIG items
        if (tweak.Category == "CONFIG")
        {
            HandleConfigToggle(tweak);
            return;
        }

        // Memory writes (PLAYER / VISUAL)
        var bytes = tweak.IsActive ? MemoryService.ParseHexString(tweak.OnBytes) : MemoryService.ParseHexString(tweak.OffBytes);
        if (_memory.WriteModuleOffset(tweak.Module, tweak.Offset, bytes))
        {
            RefreshCurrent();
            StatusMessage = tweak.IsActive ? $"✔ {tweak.Name} ativado!" : $"✘ {tweak.Name} desativado!";
            AddLog(StatusMessage);
        }
        else
        {
            var baseAddr = _memory.GetModuleBaseAddress(tweak.Module);
            var addr = baseAddr != null ? $"0x{baseAddr.Value.ToInt64() + long.Parse(tweak.Offset, System.Globalization.NumberStyles.HexNumber):X8}" : "???";
            StatusMessage = $"✘ Falha! {addr} Erro: {_memory.LastErrorCode}";
            AddLog(StatusMessage);
        }
        SaveSettings();
    }

    private void HandleConfigToggle(TweakFunction tweak)
    {
        switch (tweak.Name)
        {
            case "Iniciar com Windows":
                if (tweak.IsActive)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (RegOpenKeyEx(HKEY_CURRENT_USER, @"Software\Microsoft\Windows\CurrentVersion\Run", 0, KEY_SET_VALUE, out var hKey) != 0)
                    {
                        tweak.IsActive = false; StatusMessage = "✘ Erro ao abrir registro Run.";
                    }
                    else
                    {
                        var data = System.Text.Encoding.Unicode.GetBytes(exePath + "\0");
                        if (RegSetValueEx(hKey, "SystemService", 0, REG_SZ, data, data.Length) == 0)
                            StatusMessage = "✔ Iniciar com Windows ativado!";
                        else { tweak.IsActive = false; StatusMessage = "✘ Erro ao definir registro."; }
                        RegCloseKey(hKey);
                    }
                }
                else
                {
                    if (RegOpenKeyEx(HKEY_CURRENT_USER, @"Software\Microsoft\Windows\CurrentVersion\Run", 0, KEY_SET_VALUE, out var hKey) != 0)
                        StatusMessage = "✘ Erro ao abrir registro Run.";
                    else
                    {
                        RegDeleteValue(hKey, "SystemService");
                        RegCloseKey(hKey);
                        StatusMessage = "✘ Iniciar com Windows desativado.";
                    }
                }
                break;

            case "Auto-Conectar":
                StatusMessage = tweak.IsActive ? "✔ Auto-conectar ativado (próxima inicialização)." : "✘ Auto-conectar desativado (próxima inicialização).";
                break;

            case "Iniciar Minimizado":
                StatusMessage = tweak.IsActive ? "✔ App iniciará minimizado (próxima inicialização)." : "✘ App iniciará normal (próxima inicialização).";
                break;

            case "Auto-Update":
                StatusMessage = tweak.IsActive ? "✔ Auto-Update ativado! Verificando atualizações..." : "✘ Auto-Update desativado.";
                if (tweak.IsActive) _ = CheckUpdateAsync();
                break;
        }
        RefreshCurrent();
        SaveSettings();
    }

    private void OtimizarTudo()
    {
        int ok = 0, fail = 0;
        foreach (var tweak in Tweaks)
        {
            if (tweak.Category is not ("SISTEMAS" or "PERIFÉRICOS")) continue;
            if (tweak.IsActive) continue;
            var r = SystemOptimizer.Apply(tweak.Name);
            if (r[0] == '✔') { tweak.IsActive = true; ok++; }
            else fail++;
        }
        RefreshCurrent();
        StatusMessage = $"✔ Otimizar Tudo: {ok} ativados, {fail} falha(s)";
        AddLog(StatusMessage);
        SaveSettings();
    }

    private void RestaurarPadroes()
    {
        int ok = 0;
        foreach (var tweak in Tweaks)
        {
            if (tweak.Category is "SISTEMAS" or "PERIFÉRICOS" && tweak.IsActive)
            {
                SystemOptimizer.Restore(tweak.Name);
                tweak.IsActive = false;
                ok++;
            }
            if (tweak.Category is "BOOSTER" or "UTILITÁRIOS" or "CONFIG" or "MEMÓRIA" && tweak.IsActive)
            {
                tweak.IsActive = false;
                ok++;
            }
        }
        _islcTimer.Stop();
        _ramTimer.Stop();
        _autoAcceptTimer.Stop();
        RamInfo = "";
        var w = Application.Current.MainWindow;
        if (w != null)
        {
            var hWnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
        RefreshCurrent();
        StatusMessage = $"✔ Todos os {ok} tweaks foram restaurados ao padrão.";
        AddLog(StatusMessage);
        SaveSettings();
    }

    private void SelectAccent(object? param)
    {
        if (param is not string name) return;
        var theme = Themes.FirstOrDefault(t => t.Name == name);
        if (theme == null) return;
        ApplyTheme(theme);
        SaveSettings();
        StatusMessage = $"✔ Tema alterado para {theme.Name}!";
    }

    private static void ApplyTheme(ThemeColor t)
    {
        var res = Application.Current.Resources;
        ((SolidColorBrush)res["AccentBrush"]).Color = t.Accent;
        ((SolidColorBrush)res["AccentLightBrush"]).Color = t.Light;
        ((SolidColorBrush)res["AccentCombatBrush"]).Color = t.G1;
        ((SolidColorBrush)res["AccentVisualBrush"]).Color = t.G4;
        ((SolidColorBrush)res["AccentCyanBrush"]).Color = t.G4;
        ((SolidColorBrush)res["AccentGreenBrush"]).Color = Color.FromRgb(0x00, 0xE6, 0x76);
        ((SolidColorBrush)res["AccentSystemBrush"]).Color = t.Accent;
        ((SolidColorBrush)res["AccentMagentaBrush"]).Color = t.G2;
        ((LinearGradientBrush)res["AccentGradient"]).GradientStops[0].Color = t.Accent;
        ((LinearGradientBrush)res["AccentGradient"]).GradientStops[1].Color = t.G2;
        ((LinearGradientBrush)res["CombatGradient"]).GradientStops[0].Color = t.G1;
        ((LinearGradientBrush)res["CombatGradient"]).GradientStops[1].Color = t.G3;
        ((LinearGradientBrush)res["VisualGradient"]).GradientStops[0].Color = t.G4;
        ((LinearGradientBrush)res["VisualGradient"]).GradientStops[1].Color = t.G3;
        ((LinearGradientBrush)res["SystemGradient"]).GradientStops[0].Color = t.Accent;
        ((LinearGradientBrush)res["SystemGradient"]).GradientStops[1].Color = t.G3;
        ((DropShadowEffect)res["GlowEffect"]).Color = t.Accent;
        ((DropShadowEffect)res["GlowCombat"]).Color = t.G1;
        ((DropShadowEffect)res["GlowVisual"]).Color = t.G4;
        ((DropShadowEffect)res["GlowSystem"]).Color = t.Accent;
    }

    private void SetAll(bool activate)
    {
        int ok = 0, fail = 0;
        foreach (var tweak in Tweaks)
        {
            if (tweak.Category is "SISTEMAS" or "PERIFÉRICOS" or "BOOSTER" or "CONFIG" or "MEMÓRIA" or "UTILITÁRIOS")
            {
                if (activate && !tweak.IsActive) { tweak.IsActive = true; ok++; continue; }
                if (!activate && tweak.IsActive) { tweak.IsActive = false; ok++; continue; }
                continue;
            }
            var bytes = activate ? MemoryService.ParseHexString(tweak.OnBytes) : MemoryService.ParseHexString(tweak.OffBytes);
            if (_memory.WriteModuleOffset(tweak.Module, tweak.Offset, bytes))
            { tweak.IsActive = activate; ok++; }
            else fail++;
        }
        StatusMessage = activate ? $"✔ Ativados: {ok} ok, {fail} falha(s)" : $"✘ Desativados: {ok} ok, {fail} falha(s)";
        SaveSettings();
    }

    private static readonly string SettingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    private void LoadSettings()
    {
        try
        {
            if (!System.IO.File.Exists(SettingsPath)) return;
            var json = System.IO.File.ReadAllText(SettingsPath);
            var states = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (states == null) return;
            if (states.TryGetValue("_theme", out var themeName) && themeName is string tn)
            {
                var theme = Themes.FirstOrDefault(t => t.Name == tn);
                if (theme != null) ApplyTheme(theme);
            }
            if (states.TryGetValue("_updateUrl", out var url) && url is string updateUrl)
                AutoUpdater.UpdateCheckUrl = updateUrl;
            foreach (var t in Tweaks)
                if (states.TryGetValue(t.Name, out var v) && v is bool active)
                    t.IsActive = active;
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            var dict = new Dictionary<string, object>();
            foreach (var t in Tweaks) dict[t.Name] = t.IsActive;
            var theme = Themes.FirstOrDefault(t => ((SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color == t.Accent);
            if (theme != null) dict["_theme"] = theme.Name;
            if (!string.IsNullOrEmpty(AutoUpdater.UpdateCheckUrl)) dict["_updateUrl"] = AutoUpdater.UpdateCheckUrl;
            System.IO.File.WriteAllText(SettingsPath, JsonSerializer.Serialize(dict));
        }
        catch { }
    }

    private async Task CheckUpdateAsync()
    {
        StatusMessage = "🔄 Verificando atualizações...";
        var info = await AutoUpdater.CheckForUpdateAsync();
        if (info == null) { StatusMessage = "✘ Não foi possível verificar atualizações (URL não configurada ou offline)."; return; }
        if (info.Version == AutoUpdater.CurrentVersion)
        {
            StatusMessage = $"✔ Você já está na versão mais recente ({info.Version}).";
            AddLog(StatusMessage);
            return;
        }
        StatusMessage = $"⬇ Nova versão {info.Version} disponível! Baixando...";
        AddLog(StatusMessage);
        var result = await AutoUpdater.DownloadAndUpdateAsync(info);
        StatusMessage = result;
        AddLog(result);
        if (result.StartsWith("✔")) Application.Current.Shutdown();
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); RefreshCurrent(); }
    }

    private bool Matches(TweakFunction t)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        var s = SearchText.ToLowerInvariant();
        return t.Name.ToLowerInvariant().Contains(s) || t.Description.ToLowerInvariant().Contains(s);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
