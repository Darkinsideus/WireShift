using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WireShift;

/// <summary>Строка таблицы: один найденный конфиг.</summary>
public class ConfigEntry : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _preview = "";
    private string _status = "";
    private string _currentHost = "";

    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }

    public required string CurrentHost
    {
        get => _currentHost;
        set { _currentHost = value; OnPropertyChanged(); }
    }
    public required string Port { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>Как будет выглядеть Endpoint после замены.</summary>
    public string Preview
    {
        get => _preview;
        set { _preview = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
