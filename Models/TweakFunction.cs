using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointBlankPanel.Models;

public class TweakFunction : INotifyPropertyChanged
{
    private string _name = "";
    private string _description = "";
    private string _category = "";
    private string _icon = "🛠";
    private string _module = "";
    private string _offset = "";
    private string _offBytes = "";
    private string _onBytes = "";
    private bool _isActive;

    public string Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string Module
    {
        get => _module;
        set { _module = value; OnPropertyChanged(); }
    }

    public string Offset
    {
        get => _offset;
        set { _offset = value; OnPropertyChanged(); }
    }

    public string OffBytes
    {
        get => _offBytes;
        set { _offBytes = value; OnPropertyChanged(); }
    }

    public string OnBytes
    {
        get => _onBytes;
        set { _onBytes = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => IsActive ? "● ATIVO" : "○ DESLIGADO";

    public string AddressDisplay => string.IsNullOrEmpty(Offset) ? "—" : $"{Module}+{Offset}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
