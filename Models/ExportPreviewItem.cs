using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace CutPic.Models;

public sealed class ExportPreviewItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    public required string SourcePath { get; init; }
    public required string SourceName { get; init; }
    public required string FileName { get; init; }
    public int RegionIndex { get; init; }
    public int RegionNumber { get; init; }
    public BitmapSource? Thumbnail { get; init; }
    public string Detail { get; init; } = string.Empty;
    public string Warning { get; init; } = string.Empty;
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public string Key => $"{SourcePath}\0{RegionIndex}";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
