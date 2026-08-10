using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CutPic.Models;

public sealed class CropRegion : INotifyPropertyChanged
{
    private Rect _bounds;
    public int Number { get; set; }
    public bool RemoveBackground { get; set; } = true;
    public Rect Bounds
    {
        get => _bounds;
        set { _bounds = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
