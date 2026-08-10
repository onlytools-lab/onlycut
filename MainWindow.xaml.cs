using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CutPic.Models;
using CutPic.Services;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using System.Diagnostics;

namespace CutPic;

public partial class MainWindow : Window
{
    private sealed class ImageTask(string path, string name)
    {
        public string Path { get; } = path;
        public string Name { get; } = name;
        public string Status { get; set; } = "待检查";
        public override string ToString() => $"{(Status == "已导出" ? "↑" : Status == "已确认" ? "✓" : Status == "异常" ? "!" : "○")}  {LocalizationService.Translate(Status)} · {Name}";
    }
    private readonly record struct DetectionOptions(int Mode, double Sensitivity, int Grouping, double MinimumArea, int Padding);
    private sealed class EditHistory
    {
        public Stack<List<RegionSnapshot>> Undo { get; } = new();
        public Stack<List<RegionSnapshot>> Redo { get; } = new();
    }
    private readonly record struct RegionSnapshot(Rect Bounds, bool RemoveBackground);
    private List<CropRegion> _regions = [];
    private readonly List<ImageTask> _tasks = [];
    private readonly Dictionary<string, List<CropRegion>> _regionStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EditHistory> _histories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _longModeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Color> _manualBackgroundColors = new(StringComparer.OrdinalIgnoreCase);
    private BitmapSource? _bitmap;
    private string? _sourcePath;
    private CropRegion? _selected;
    private Point _dragStart;
    private Rect _dragOriginal;
    private Rect _dragNextOriginal;
    private DragMode _dragMode;
    private double _zoom = 1;
    private bool _suppressWorkModeChange;
    private bool _suppressRegionBackgroundChange;
    private bool _isPickingBackgroundColor;
    private bool _autoRestoreSession = true;
    private int _autoSaveIntervalSeconds = 15;
    private string _fontFamilyName = "System";
    private string _languageCode = "zh-CN";
    private CancellationTokenSource? _exportCancellation;
    private bool _isExporting;
    private readonly record struct ExportProgressInfo(int Current, int Total, string FileName);
    private sealed class ExportSummary
    {
        public int Success { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public bool Cancelled { get; set; }
        public List<string> Errors { get; } = [];
    }
    private readonly DispatcherTimer _recoveryTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private string? _currentProjectPath;
    private bool IsLongMode => WorkModeBox.SelectedIndex == 1;
    private enum DragMode { None, Move, Resize }

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closing += (_, _) => { SaveSettings(); SaveRecoveryProject(); };
        LoadSettings();
        LocalizationService.SetLanguage(_languageCode);
        LocalizationService.Apply(this);
        ApplyFontFamily();
        StatusText.LayoutUpdated += (_, _) =>
        {
            string translated = LocalizationService.Translate(StatusText.Text);
            if (translated != StatusText.Text) StatusText.Text = translated;
        };
        LongSplitModeBox_SelectionChanged(this, null!);
        _recoveryTimer.Interval = TimeSpan.FromSeconds(_autoSaveIntervalSeconds);
        _recoveryTimer.Tick += (_, _) => SaveRecoveryProject();
        _recoveryTimer.Start();
        if (_autoRestoreSession)
            Dispatcher.BeginInvoke(() => RestoreRecoveryProject(), DispatcherPriority.ApplicationIdle);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*", Multiselect = true };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new AppSettings
        {
            ConflictPolicyIndex = ConflictPolicyBox.SelectedIndex,
            JpegQuality = QualitySlider.Value,
            NamingModeIndex = NamingModeBox.SelectedIndex,
            NamingPrefix = NamingPrefixBox.Text,
            NamingSuffix = NamingSuffixBox.Text,
            NamingStartNumber = NamingStartNumberBox.Text,
            NamingDigitsIndex = NamingDigitsBox.SelectedIndex,
            CreateSourceSubfolder = CreateSourceSubfolderBox.IsChecked == true,
            OpenFolderAfterExport = OpenFolderAfterExportBox.IsChecked == true,
            AutoRestoreSession = _autoRestoreSession,
            AutoSaveIntervalSeconds = _autoSaveIntervalSeconds,
            FontFamilyName = _fontFamilyName,
            LanguageCode = _languageCode
        };
        var dialog = new SettingsWindow(settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        settings = dialog.Settings;
        ConflictPolicyBox.SelectedIndex = settings.ConflictPolicyIndex;
        QualitySlider.Value = settings.JpegQuality;
        NamingModeBox.SelectedIndex = settings.NamingModeIndex;
        NamingPrefixBox.Text = settings.NamingPrefix;
        NamingSuffixBox.Text = settings.NamingSuffix;
        NamingStartNumberBox.Text = settings.NamingStartNumber;
        NamingDigitsBox.SelectedIndex = settings.NamingDigitsIndex;
        CreateSourceSubfolderBox.IsChecked = settings.CreateSourceSubfolder;
        OpenFolderAfterExportBox.IsChecked = settings.OpenFolderAfterExport;
        _autoRestoreSession = settings.AutoRestoreSession;
        _autoSaveIntervalSeconds = settings.AutoSaveIntervalSeconds;
        _fontFamilyName = settings.FontFamilyName;
        _languageCode = settings.LanguageCode;
        LocalizationService.SetLanguage(_languageCode);
        _recoveryTimer.Interval = TimeSpan.FromSeconds(_autoSaveIntervalSeconds);
        ApplyFontFamily();
        LocalizationService.Apply(this);
        TaskList.Items.Refresh();
        UpdateNamingExample();
        SaveSettings();
        StatusText.Text = "设置已保存";
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择包含图片的文件夹" };
        if (dialog.ShowDialog() != true) return;
        string[] extensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp"];
        var files = Directory.EnumerateFiles(dialog.FolderName, "*.*", new EnumerationOptions
            { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System })
            .Where(path => extensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        AddFiles(files);
        StatusText.Text = files.Length == 0 ? "文件夹中没有找到支持的图片" : $"已从文件夹导入 {files.Length} 张图片";
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        ImageTask? firstAdded = null;
        foreach (string path in paths.Where(File.Exists))
        {
            if (_tasks.Any(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            var task = new ImageTask(path, System.IO.Path.GetFileName(path));
            _longModeStates[path] = IsLongMode;
            _tasks.Add(task); TaskList.Items.Add(task); firstAdded ??= task;
        }
        if (firstAdded is not null) TaskList.SelectedItem = firstAdded;
        StatusText.Text = $"任务队列中共有 {_tasks.Count} 张图片";
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存 CutPic 项目", Filter = "CutPic 项目|*.cutpic|JSON 文件|*.json",
            DefaultExt = ".cutpic", FileName = _currentProjectPath is null ? "CutPic项目.cutpic" : System.IO.Path.GetFileName(_currentProjectPath)
        };
        if (dialog.ShowDialog() != true) return;
        SaveProjectDocument(dialog.FileName);
        _currentProjectPath = dialog.FileName;
        StatusText.Text = $"项目已保存：{dialog.FileName}";
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "打开 CutPic 项目", Filter = "CutPic 项目|*.cutpic;*.json|所有文件|*.*" };
        if (dialog.ShowDialog() != true) return;
        try { LoadProjectDocument(dialog.FileName); _currentProjectPath = dialog.FileName; }
        catch (Exception ex)
        { ShowMessage( $"项目文件无法打开：\n{ex.Message}", "打开项目失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static string LocalDataDirectory => Environment.GetEnvironmentVariable("CUTPIC_DATA_DIR")
        ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CutPic");
    private static string RecoveryProjectPath => System.IO.Path.Combine(LocalDataDirectory, "last-session.cutpic");

    private ProjectDocument BuildProjectDocument() => new()
    {
        SavedAt = DateTimeOffset.Now,
        Tasks = _tasks.Select(task => new ProjectTaskDocument
        {
            ImagePath = task.Path,
            IsLongMode = _longModeStates.TryGetValue(task.Path, out bool isLong) && isLong,
            SampledBackgroundColor = _manualBackgroundColors.TryGetValue(task.Path, out Color sampled)
                ? sampled.ToString() : null,
            Status = task.Status,
            Regions = (_regionStates.TryGetValue(task.Path, out var regions) ? regions : [])
                .Select(region => new CropBoundsDocument
                {
                    X = region.Bounds.X, Y = region.Bounds.Y,
                    Width = region.Bounds.Width, Height = region.Bounds.Height,
                    RemoveBackground = region.RemoveBackground
                }).ToList()
        }).ToList()
    };

    private void SaveProjectDocument(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(BuildProjectDocument(), new JsonSerializerOptions { WriteIndented = true }));
    }

    private void SaveRecoveryProject()
    { try { SaveProjectDocument(RecoveryProjectPath); } catch { } }

    private void RestoreRecoveryProject()
    {
        if (_tasks.Count > 0 || !File.Exists(RecoveryProjectPath)) return;
        try
        {
            var document = JsonSerializer.Deserialize<ProjectDocument>(File.ReadAllText(RecoveryProjectPath));
            if (document?.Tasks.Count > 0) LoadProjectDocument(RecoveryProjectPath, true);
        }
        catch { }
    }

    private void LoadProjectDocument(string path, bool isRecovery = false)
    {
        var document = JsonSerializer.Deserialize<ProjectDocument>(File.ReadAllText(path))
            ?? throw new InvalidDataException("项目文件内容为空或格式无效。");
        TaskList.Items.Clear(); _tasks.Clear(); _regionStates.Clear(); _longModeStates.Clear(); _histories.Clear();
        _manualBackgroundColors.Clear();
        int missing = 0;
        foreach (var savedTask in document.Tasks)
        {
            if (!File.Exists(savedTask.ImagePath)) { missing++; continue; }
            var task = new ImageTask(savedTask.ImagePath, System.IO.Path.GetFileName(savedTask.ImagePath)) { Status = savedTask.Status };
            _tasks.Add(task); TaskList.Items.Add(task); _longModeStates[task.Path] = savedTask.IsLongMode;
            if (!string.IsNullOrWhiteSpace(savedTask.SampledBackgroundColor))
            {
                try { _manualBackgroundColors[task.Path] = (Color)ColorConverter.ConvertFromString(savedTask.SampledBackgroundColor)!; }
                catch { }
            }
            if (savedTask.Regions.Count > 0)
                _regionStates[task.Path] = savedTask.Regions.Select((bounds, index) => new CropRegion
                {
                    Number = index + 1,
                    Bounds = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                    RemoveBackground = bounds.RemoveBackground
                }).ToList();
        }
        TaskList.Items.Refresh();
        if (_tasks.Count > 0) TaskList.SelectedIndex = 0;
        StatusText.Text = isRecovery
            ? $"已自动恢复上次会话：{_tasks.Count} 个任务"
            : $"项目已打开：{_tasks.Count} 个任务" + (missing > 0 ? $"，{missing} 个源文件缺失" : string.Empty);
    }

    private void LoadFile(string path)
    {
        try
        {
            _bitmap = ImageProcessingService.LoadImage(path);
            _sourcePath = path;
            SourceImage.Source = _bitmap;
            EmptyState.Visibility = Visibility.Collapsed;
            SourceImage.Width = Overlay.Width = ImageHost.Width = _bitmap.PixelWidth;
            SourceImage.Height = Overlay.Height = ImageHost.Height = _bitmap.PixelHeight;
            UpdateSampledBackgroundUi();
            ImageInfoText.Text = $"{System.IO.Path.GetFileName(path)}  ·  {_bitmap.PixelWidth} × {_bitmap.PixelHeight}";
            if (_regionStates.TryGetValue(path, out var saved))
            {
                _regions = saved;
                _selected = _regions.FirstOrDefault();
                if (!IsLongMode) InferGridParameters(false);
                RenderRegions();
            }
            else if (IsLongMode) GenerateLongSlices(false);
            else DetectRegions();
        }
        catch (Exception ex) { ShowMessage( $"无法打开图片：\n{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Detect_Click(object sender, RoutedEventArgs e) => DetectRegions();
    private void DetectRegions()
    {
        if (_bitmap is null) return;
        if (IsLongMode) { GenerateLongSlices(true); return; }
        try
        {
            if (_regions.Count > 0) PushHistory();
            _regions.Clear();
            _regions.AddRange(DetectUsingOptions(_bitmap, ReadDetectionOptions()));
            if (_sourcePath is not null) _regionStates[_sourcePath] = _regions;
            _selected = _regions.FirstOrDefault();
            InferGridParameters(false);
            RenderRegions();
            StatusText.Text = _regions.Count > 0
                ? $"已自动识别 {_regions.Count} 个切片区域，可拖动、缩放或调整识别参数"
                : "没有识别到元素，请提高灵敏度、调整合并距离或手动添加选框";
        }
        catch (Exception ex) { StatusText.Text = $"识别失败：{ex.Message}"; }
    }

    private void RenderRegions(bool updatePreview = true)
    {
        Overlay.Children.Clear();
        foreach (var region in _regions)
        {
            bool selected = ReferenceEquals(region, _selected);
            var border = new Border
            {
                Tag = region, Width = region.Bounds.Width, Height = region.Bounds.Height,
                BorderThickness = new Thickness(selected ? 3 : 2),
                BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(255, 190, 55) : Color.FromRgb(47, 211, 255)),
                Background = new SolidColorBrush(Color.FromArgb(selected ? (byte)24 : (byte)12, 50, 190, 255)),
                Cursor = IsLongMode ? Cursors.Arrow : Cursors.SizeAll
            };
            border.Child = new Grid
            {
                Children =
                {
                    new Border { Background = new SolidColorBrush(Color.FromArgb(210, 15, 18, 24)), CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6,2,6,2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                        Child = new TextBlock { Text = region.RemoveBackground ? region.Number.ToString() : $"{region.Number} · 保留背景", Foreground = Brushes.White, FontWeight = FontWeights.Bold } },
                    new Ellipse { Width = IsLongMode ? region.Bounds.Width : 14, Height = IsLongMode ? 5 : 14,
                        Fill = Brushes.White, Stroke = Brushes.DodgerBlue, StrokeThickness = IsLongMode ? 1 : 2,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = IsLongMode ? new Thickness(0,0,0,-3) : new Thickness(0,0,-7,-7),
                        Cursor = IsLongMode ? Cursors.SizeNS : Cursors.SizeNWSE, Tag = "Handle" }
                }
            };
            border.PreviewMouseLeftButtonDown += Region_MouseLeftButtonDown;
            Canvas.SetLeft(border, region.Bounds.X); Canvas.SetTop(border, region.Bounds.Y);
            Overlay.Children.Add(border);
        }
        UpdateBoundsBoxes();
        _suppressRegionBackgroundChange = true;
        if (RemoveBackgroundForRegionBox is not null)
        {
            RemoveBackgroundForRegionBox.IsEnabled = _selected is not null;
            RemoveBackgroundForRegionBox.IsChecked = _selected?.RemoveBackground == true;
        }
        _suppressRegionBackgroundChange = false;
        if (updatePreview) UpdateOutputPreview();
    }

    private void Region_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not CropRegion region) return;
        PushHistory();
        _selected = region;
        _dragStart = e.GetPosition(Overlay);
        _dragOriginal = region.Bounds;
        int longIndex = _regions.IndexOf(region);
        _dragNextOriginal = IsLongMode && longIndex >= 0 && longIndex < _regions.Count - 1
            ? _regions[longIndex + 1].Bounds : Rect.Empty;
        var local = e.GetPosition(border);
        _dragMode = IsLongMode
            ? (local.Y >= border.ActualHeight - 30 ? DragMode.Resize : DragMode.None)
            : (local.X >= border.ActualWidth - 24 && local.Y >= border.ActualHeight - 24 ? DragMode.Resize : DragMode.Move);
        Overlay.CaptureMouse();
        RenderRegions();
        e.Handled = true;
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.None || _selected is null || _bitmap is null || e.LeftButton != MouseButtonState.Pressed) return;
        Point now = e.GetPosition(Overlay);
        double dx = now.X - _dragStart.X, dy = now.Y - _dragStart.Y;
        if (IsLongMode)
        {
            int index = _regions.IndexOf(_selected);
            if (_dragMode != DragMode.Resize || index < 0 || index >= _regions.Count - 1) return;
            var next = _regions[index + 1];
            double combined = _dragOriginal.Height + _dragNextOriginal.Height;
            double newHeight = Math.Round(Math.Clamp(_dragOriginal.Height + dy, 20, combined - 20));
            _selected.Bounds = new Rect(0, _dragOriginal.Y, _bitmap.PixelWidth, newHeight);
            next.Bounds = new Rect(0, _dragOriginal.Y + newHeight, _bitmap.PixelWidth, combined - newHeight);
            RenderRegions(false);
            return;
        }
        if (_dragMode == DragMode.Move)
            _selected.Bounds = new Rect(Math.Clamp(_dragOriginal.X + dx, 0, _bitmap.PixelWidth - _dragOriginal.Width),
                Math.Clamp(_dragOriginal.Y + dy, 0, _bitmap.PixelHeight - _dragOriginal.Height), _dragOriginal.Width, _dragOriginal.Height);
        else
            _selected.Bounds = new Rect(_dragOriginal.X, _dragOriginal.Y,
                Math.Clamp(_dragOriginal.Width + dx, 20, _bitmap.PixelWidth - _dragOriginal.X),
                Math.Clamp(_dragOriginal.Height + dy, 20, _bitmap.PixelHeight - _dragOriginal.Y));
        RenderRegions(false);
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    { _dragMode = DragMode.None; Overlay.ReleaseMouseCapture(); UpdateOutputPreview(); }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (e.OriginalSource == Overlay) { _selected = null; RenderRegions(); } }

    private void Overlay_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPickingBackgroundColor || _bitmap is null || _sourcePath is null) return;
        Point point = e.GetPosition(Overlay);
        Color sampled = ImageProcessingService.SampleColor(_bitmap, (int)Math.Round(point.X), (int)Math.Round(point.Y));
        _manualBackgroundColors[_sourcePath] = sampled;
        _isPickingBackgroundColor = false;
        Overlay.Cursor = Cursors.Arrow;
        UpdateSampledBackgroundUi();
        UpdateOutputPreview();
        StatusText.Text = $"已吸取背景色 {sampled}";
        e.Handled = true;
    }

    private void AddRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmap is null) return;
        if (IsLongMode)
        {
            if (_selected is null || _selected.Bounds.Height < 40) return;
            PushHistory();
            int index = _regions.IndexOf(_selected);
            double firstHeight = Math.Floor(_selected.Bounds.Height / 2);
            double secondHeight = _selected.Bounds.Height - firstHeight;
            _selected.Bounds = new Rect(0, _selected.Bounds.Y, _bitmap.PixelWidth, firstHeight);
            _regions.Insert(index + 1, new CropRegion { Bounds = new Rect(0, _selected.Bounds.Bottom, _bitmap.PixelWidth, secondHeight) });
            RenumberRegions(); RenderRegions(); return;
        }
        PushHistory();
        double size = Math.Min(_bitmap.PixelWidth, _bitmap.PixelHeight) * .3;
        var region = new CropRegion { Number = _regions.Count + 1,
            Bounds = new Rect((_bitmap.PixelWidth - size) / 2, (_bitmap.PixelHeight - size) / 2, size, size) };
        _regions.Add(region); _selected = region; RenderRegions();
        StatusText.Text = "已添加选框，请拖动到目标位置并调整大小";
    }

    private void DeleteRegion_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    private void DeleteSelected()
    {
        if (_selected is null) return;
        if (IsLongMode)
        {
            if (_regions.Count <= 1) return;
            PushHistory();
            int index = _regions.IndexOf(_selected);
            if (index < _regions.Count - 1)
            {
                var next = _regions[index + 1];
                next.Bounds = new Rect(0, _selected.Bounds.Y, next.Bounds.Width, _selected.Bounds.Height + next.Bounds.Height);
            }
            else
            {
                var previous = _regions[index - 1];
                previous.Bounds = new Rect(0, previous.Bounds.Y, previous.Bounds.Width, previous.Bounds.Height + _selected.Bounds.Height);
            }
            _regions.RemoveAt(index); _selected = _regions[Math.Min(index, _regions.Count - 1)];
            RenumberRegions(); RenderRegions(); return;
        }
        PushHistory();
        _regions.Remove(_selected); _selected = null;
        for (int i = 0; i < _regions.Count; i++) _regions[i].Number = i + 1;
        RenderRegions();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_isExporting) return;
        if (_tasks.Count == 0) { ShowMessage( "请先添加图片并确认切割框。", "无法导出"); return; }
        var dialog = new OpenFolderDialog { Title = "选择导出文件夹" };
        if (dialog.ShowDialog() != true) return;
        string exportDirectory = dialog.FolderName;
        bool transparentBackground = TransparentBackgroundBox.IsChecked == true && !IsLongMode;
        string format = transparentBackground ? "PNG" : ((ComboBoxItem)FormatBox.SelectedItem).Content?.ToString() ?? "PNG";
        string extension = format == "JPG" ? ".jpg" : ".png";
        int outputSize = int.TryParse(((ComboBoxItem)SizeBox.SelectedItem).Tag?.ToString(), out int parsed) ? parsed : 512;
        bool autoBackground = AutoBackgroundBox.IsChecked == true;
        int jpegQuality = (int)QualitySlider.Value;
        int transparencyTolerance = (int)TransparencyToleranceSlider.Value;
        int transparencyFeather = (int)TransparencyFeatherSlider.Value;
        var transparencyBackgroundSource = (TransparencyBackgroundSource)Math.Clamp(
            TransparencyBackgroundSourceBox.SelectedIndex, 0, 2);
        int conflictPolicy = ConflictPolicyBox.SelectedIndex;
        DetectionOptions detectionOptions = ReadDetectionOptions();
        int longSplitMode = LongSplitModeBox.SelectedIndex;
        int longCount = int.TryParse(LongCountBox.Text, out var parsedCount) ? parsedCount : 3;
        int longFixedHeight = int.TryParse(LongFixedHeightBox.Text, out var parsedHeight) ? parsedHeight : 1200;
        int[] longCustomHeights = ParseHeightList(LongCustomHeightsBox.Text).ToArray();
        int smartTargetHeight = int.TryParse(SmartTargetHeightBox.Text, out var parsedTarget) ? parsedTarget : 1200;
        int smartSearchRange = int.TryParse(SmartSearchRangeBox.Text, out var parsedRange) ? parsedRange : 240;
        int namingMode = NamingModeBox.SelectedIndex;
        string namingPrefix = NamingPrefixBox.Text.Trim();
        string namingSuffix = NamingSuffixBox.Text.Trim();
        int namingStart = int.TryParse(NamingStartNumberBox.Text, out int parsedStart) ? Math.Max(0, parsedStart) : 1;
        int namingDigits = int.TryParse(((ComboBoxItem)NamingDigitsBox.SelectedItem).Tag?.ToString(), out int parsedDigits)
            ? Math.Clamp(parsedDigits, 1, 8) : 2;
        bool createSourceSubfolder = CreateSourceSubfolderBox.IsChecked == true;
        bool openFolderAfterExport = OpenFolderAfterExportBox.IsChecked == true;
        _isExporting = true;
        _exportCancellation = new CancellationTokenSource();
        var token = _exportCancellation.Token;
        WorkspaceGrid.IsEnabled = false; AppBarActions.IsEnabled = false;
        ExportProgressPanel.Visibility = Visibility.Visible;
        ExportProgressBar.IsIndeterminate = true;
        int completed = 0;
        int estimatedTotal = Math.Max(1, _regionStates.Values.Sum(regions => regions.Count));
        var failures = new List<string>();
        int succeeded = 0, skipped = 0, failed = 0;
        IProgress<ExportProgressInfo> progress = new Progress<ExportProgressInfo>(value =>
        {
            ExportProgressBar.IsIndeterminate = false;
            ExportProgressBar.Maximum = Math.Max(1, value.Total);
            ExportProgressBar.Value = value.Current;
            ExportProgressText.Text = $"{value.Current} / {value.Total}";
            StatusText.Text = $"正在导出：{value.FileName}";
        });
        try
        {
            int total = 0;
            await Task.Run(() =>
            {
                foreach (var task in _tasks)
                {
                    if (token.IsCancellationRequested) break;
                    try
                    {
                    var bitmap = ImageProcessingService.LoadImage(task.Path);
                    bool isLongTask = _longModeStates.TryGetValue(task.Path, out bool taskMode) && taskMode;
                    var regions = _regionStates.TryGetValue(task.Path, out var saved)
                        ? saved
                        : isLongTask
                            ? longSplitMode == 3
                                ? ImageProcessingService.SuggestLongImageSlices(bitmap, smartTargetHeight, smartSearchRange)
                                : BuildLongRegions(bitmap.PixelWidth, bitmap.PixelHeight, longSplitMode, longCount, longFixedHeight, longCustomHeights)
                            : DetectUsingOptions(bitmap, detectionOptions).ToList();
                    var validationErrors = ValidateRegions(bitmap, regions, isLongTask);
                    if (validationErrors.Count > 0)
                    {
                        task.Status = "异常";
                        throw new InvalidDataException($"{task.Name} 导出检查失败：{string.Join("；", validationErrors)}");
                    }
                    string baseName = System.IO.Path.GetFileNameWithoutExtension(task.Path);
                    string taskOutputDirectory = createSourceSubfolder
                        ? System.IO.Path.Combine(exportDirectory, SanitizeFileName(baseName, "图片"))
                        : exportDirectory;
                    Directory.CreateDirectory(taskOutputDirectory);
                    estimatedTotal = Math.Max(estimatedTotal, completed + regions.Count);
                    int failuresBeforeTask = failed;
                    for (int i = 0; i < regions.Count; i++)
                    {
                        if (token.IsCancellationRequested) break;
                        progress.Report(new ExportProgressInfo(completed, estimatedTotal, task.Name));
                        try
                        {
                        var output = isLongTask
                            ? ImageProcessingService.CreateCrop(bitmap, regions[i].Bounds)
                            : transparentBackground && regions[i].RemoveBackground
                                ? ImageProcessingService.CreateTransparentSquare(bitmap, regions[i].Bounds, outputSize,
                                    transparencyTolerance, transparencyFeather, transparencyBackgroundSource,
                                    _manualBackgroundColors.TryGetValue(task.Path, out Color sampled) ? sampled : null)
                                : ImageProcessingService.CreateSquare(bitmap, regions[i].Bounds, outputSize, autoBackground, Colors.White);
                        string fileName = BuildExportFileName(baseName, isLongTask, i, extension, namingMode,
                            namingPrefix, namingSuffix, namingStart, namingDigits);
                        string requestedPath = System.IO.Path.Combine(taskOutputDirectory, fileName);
                        string? path = ResolveOutputPath(requestedPath, conflictPolicy);
                        if (path is null) { skipped++; completed++; continue; }
                        ImageProcessingService.Save(output, path, format, jpegQuality);
                        total++; succeeded++;
                        }
                        catch (Exception ex)
                        {
                            failed++; failures.Add($"{task.Name} / #{i + 1}: {ex.Message}");
                        }
                        completed++;
                    }
                    task.Status = failed > failuresBeforeTask ? "异常" : "已导出";
                    }
                    catch (Exception ex)
                    {
                        task.Status = "异常"; failed++; failures.Add($"{task.Name}: {ex.Message}");
                    }
                }
            });
            TaskList.Items.Refresh();
            bool cancelled = token.IsCancellationRequested;
            string detail = failures.Count == 0 ? string.Empty : "\n\n失败明细：\n" + string.Join("\n", failures.Take(15)) +
                (failures.Count > 15 ? $"\n……另有 {failures.Count - 15} 项" : string.Empty);
            string message = $"成功：{succeeded}\n跳过：{skipped}\n失败：{failed}" + detail;
            StatusText.Text = cancelled ? "导出已取消，已完成的文件予以保留" : "批量导出完成";
            ShowMessage(message, cancelled ? "导出已取消" : "导出结果汇总", MessageBoxButton.OK,
                failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            if (openFolderAfterExport && succeeded > 0) OpenFolder(exportDirectory);
        }
        catch (Exception ex) { TaskList.Items.Refresh(); ShowMessage($"导出失败：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally
        {
            _exportCancellation?.Dispose(); _exportCancellation = null; _isExporting = false;
            WorkspaceGrid.IsEnabled = true; AppBarActions.IsEnabled = true;
            ExportProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelExport_Click(object sender, RoutedEventArgs e)
    {
        _exportCancellation?.Cancel();
        StatusText.Text = "正在取消导出，请等待当前切片处理完成……";
    }

    private static List<string> ValidateRegions(BitmapSource bitmap, IReadOnlyList<CropRegion> regions, bool longMode)
    {
        var errors = new List<string>();
        if (regions.Count == 0) { errors.Add("没有可导出的切片"); return errors; }
        const double epsilon = .01;
        foreach (var region in regions)
        {
            var bounds = region.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) errors.Add($"切片 {region.Number} 尺寸无效");
            if (bounds.X < -epsilon || bounds.Y < -epsilon || bounds.Right > bitmap.PixelWidth + epsilon || bounds.Bottom > bitmap.PixelHeight + epsilon)
                errors.Add($"切片 {region.Number} 超出图片范围");
        }
        if (!longMode) return errors.Distinct().ToList();
        var ordered = regions.OrderBy(r => r.Bounds.Y).ToList();
        if (Math.Abs(ordered[0].Bounds.Y) > epsilon) errors.Add("长图顶部未被覆盖");
        for (int i = 0; i < ordered.Count; i++)
        {
            if (Math.Abs(ordered[i].Bounds.X) > epsilon || Math.Abs(ordered[i].Bounds.Width - bitmap.PixelWidth) > epsilon)
                errors.Add($"切片 {ordered[i].Number} 未保持 100% 原图宽度");
            if (i > 0 && Math.Abs(ordered[i - 1].Bounds.Bottom - ordered[i].Bounds.Y) > epsilon)
                errors.Add($"切片 {ordered[i - 1].Number} 与 {ordered[i].Number} 之间存在间距或重叠");
        }
        if (Math.Abs(ordered[^1].Bounds.Bottom - bitmap.PixelHeight) > epsilon) errors.Add("长图底部未被完整覆盖");
        return errors.Distinct().ToList();
    }

    private static string? ResolveOutputPath(string requestedPath, int policy)
    {
        if (!File.Exists(requestedPath) || policy == 1) return requestedPath;
        if (policy == 2) return null;
        string directory = System.IO.Path.GetDirectoryName(requestedPath)!;
        string name = System.IO.Path.GetFileNameWithoutExtension(requestedPath);
        string extension = System.IO.Path.GetExtension(requestedPath);
        for (int number = 1; ; number++)
        {
            string candidate = System.IO.Path.Combine(directory, $"{name} ({number}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static string BuildExportFileName(string sourceBaseName, bool isLong, int zeroBasedIndex,
        string extension, int namingMode, string prefix, string suffix, int startNumber, int digits)
    {
        string root = namingMode == 1 ? SanitizeFileName(prefix, "切片") : SanitizeFileName(sourceBaseName, "图片");
        string safeSuffix = string.IsNullOrWhiteSpace(suffix) ? string.Empty : "_" + SanitizeFileName(suffix, string.Empty);
        string separator = isLong ? "_part_" : "_";
        string number = (startNumber + zeroBasedIndex).ToString($"D{digits}", CultureInfo.InvariantCulture);
        string stem = $"{root}{separator}{number}{safeSuffix}";
        if (stem.Length > 180) stem = stem[..180].TrimEnd(' ', '.');
        return stem + extension;
    }

    private static string SanitizeFileName(string value, string fallback)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars().ToHashSet();
        string result = new(value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        result = result.Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(result)) result = fallback;
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        if (reserved.Contains(result, StringComparer.OrdinalIgnoreCase)) result = "_" + result;
        return result;
    }

    private void OpenFolder(string directory)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true }); }
        catch (Exception ex) { StatusText.Text = $"导出完成，但无法打开文件夹：{ex.Message}"; }
    }

    private void GenerateGrid_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmap is null) return;
        if (!int.TryParse(RowsBox.Text, out int rows) || !int.TryParse(ColumnsBox.Text, out int columns) ||
            !double.TryParse(MarginXBox.Text, out double marginX) || !double.TryParse(MarginYBox.Text, out double marginY) ||
            !double.TryParse(GapXBox.Text, out double gapX) || !double.TryParse(GapYBox.Text, out double gapY) ||
            rows is < 1 or > 20 || columns is < 1 or > 20)
        { ShowMessage( "请输入有效的行列、边距和间距。行列范围为 1–20。", "参数无效"); return; }
        double cellWidth = (_bitmap.PixelWidth - marginX * 2 - gapX * (columns - 1)) / columns;
        double cellHeight = (_bitmap.PixelHeight - marginY * 2 - gapY * (rows - 1)) / rows;
        if (cellWidth < 20 || cellHeight < 20) { ShowMessage( "边距或间距过大，无法生成有效选框。", "参数无效"); return; }
        PushHistory();
        _regions.Clear();
        int number = 1;
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
            _regions.Add(new CropRegion { Number = number++, Bounds = new Rect(
                marginX + column * (cellWidth + gapX), marginY + row * (cellHeight + gapY), cellWidth, cellHeight) });
        if (_sourcePath is not null) _regionStates[_sourcePath] = _regions;
        _selected = _regions.FirstOrDefault(); RenderRegions();
        StatusText.Text = $"已生成 {columns} 列 × {rows} 行，共 {_regions.Count} 个选框";
    }

    private void WorkModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecognitionExpander is null) return;
        UpdateWorkModeUi();
        if (_suppressWorkModeChange || _bitmap is null || _sourcePath is null) return;
        _longModeStates[_sourcePath] = IsLongMode;
        _regionStates.Remove(_sourcePath);
        _histories.Remove(_sourcePath);
        _regions = [];
        if (IsLongMode) GenerateLongSlices(false); else DetectRegions();
    }

    private void UpdateWorkModeUi()
    {
        if (RecognitionExpander is null) return;
        RecognitionExpander.Visibility = IsLongMode ? Visibility.Collapsed : Visibility.Visible;
        FixedGridExpander.Visibility = IsLongMode ? Visibility.Collapsed : Visibility.Visible;
        LongImageExpander.Visibility = IsLongMode ? Visibility.Visible : Visibility.Collapsed;
        SizeBox.IsEnabled = !IsLongMode;
        AutoBackgroundBox.IsEnabled = !IsLongMode;
        TransparentBackgroundBox.IsEnabled = !IsLongMode;
        CleanCornersBox.IsEnabled = false;
        XBox.IsEnabled = !IsLongMode; YBox.IsEnabled = !IsLongMode; WidthBox.IsEnabled = !IsLongMode;
        TransparentBackgroundBox_Click(this, null!);
    }

    private void LongSplitModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LongCountBox is null) return;
        LongCountBox.Visibility = LongSplitModeBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        LongFixedHeightBox.Visibility = LongSplitModeBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        LongCustomHeightsBox.Visibility = LongSplitModeBox.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        SmartLongOptionsPanel.Visibility = LongSplitModeBox.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GenerateLongSlices_Click(object sender, RoutedEventArgs e) => GenerateLongSlices(true);

    private void GenerateLongSlices(bool pushHistory)
    {
        if (_bitmap is null) return;
        int mode = LongSplitModeBox.SelectedIndex;
        if (!int.TryParse(LongCountBox.Text, out int count)) count = 3;
        if (!int.TryParse(LongFixedHeightBox.Text, out int fixedHeight)) fixedHeight = 1200;
        int[] custom = ParseHeightList(LongCustomHeightsBox.Text).ToArray();
        try
        {
            int smartTarget = int.TryParse(SmartTargetHeightBox.Text, out var target) ? target : 1200;
            int smartRange = int.TryParse(SmartSearchRangeBox.Text, out var range) ? range : 240;
            var generated = mode == 3
                ? ImageProcessingService.SuggestLongImageSlices(_bitmap, smartTarget, smartRange)
                : BuildLongRegions(_bitmap.PixelWidth, _bitmap.PixelHeight, mode, count, fixedHeight, custom);
            if (pushHistory && _regions.Count > 0) PushHistory();
            _regions = generated;
            if (_sourcePath is not null)
            {
                _regionStates[_sourcePath] = _regions;
                _longModeStates[_sourcePath] = true;
            }
            _selected = _regions.FirstOrDefault(); RenderRegions();
            StatusText.Text = $"已将长图连续切分为 {_regions.Count} 段，宽度均为 {_bitmap.PixelWidth}px";
        }
        catch (ArgumentException ex)
        {
            ShowMessage( ex.Message, "长图切分参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal static List<CropRegion> BuildLongRegions(int width, int totalHeight, int mode,
        int count, int fixedHeight, IReadOnlyList<int> customHeights)
    {
        var heights = new List<int>();
        if (mode == 0)
        {
            if (count < 1 || count > totalHeight) throw new ArgumentException("切片数量必须大于 0，且不能超过图片像素高度。");
            for (int i = 0; i < count; i++)
            {
                int start = (int)((long)i * totalHeight / count);
                int end = (int)((long)(i + 1) * totalHeight / count);
                heights.Add(end - start);
            }
        }
        else if (mode == 1)
        {
            if (fixedHeight < 1) throw new ArgumentException("固定高度必须大于 0。");
            int remaining = totalHeight;
            while (remaining > 0) { int h = Math.Min(fixedHeight, remaining); heights.Add(h); remaining -= h; }
        }
        else if (mode == 2)
        {
            if (customHeights.Count == 0) throw new ArgumentException("请至少输入一个自定义高度，每行一个数字。");
            int used = 0;
            foreach (int height in customHeights)
            {
                if (height < 1) throw new ArgumentException("每一段的高度都必须大于 0。");
                if (used + height >= totalHeight)
                    throw new ArgumentException("自定义高度之和必须小于图片总高度，以便最后一段使用剩余高度。");
                heights.Add(height); used += height;
            }
            heights.Add(totalHeight - used);
        }
        else throw new ArgumentException("未知的长图切分方式。");

        var result = new List<CropRegion>();
        int y = 0;
        for (int i = 0; i < heights.Count; i++)
        {
            result.Add(new CropRegion { Number = i + 1, Bounds = new Rect(0, y, width, heights[i]) });
            y += heights[i];
        }
        return result;
    }

    private static IEnumerable<int> ParseHeightList(string text) => text
        .Split(['\r', '\n', ',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
        .Select(value => int.TryParse(value.Trim(), out int height) ? height : -1);

    private void RenumberRegions()
    {
        for (int i = 0; i < _regions.Count; i++) _regions[i].Number = i + 1;
        if (_sourcePath is not null) _regionStates[_sourcePath] = _regions;
    }

    private void UpdateOutputPreview()
    {
        if (_bitmap is null || _selected is null) { OutputPreview.Source = null; return; }
        try { OutputPreview.Source = IsLongMode
            ? ImageProcessingService.CreateCrop(_bitmap, _selected.Bounds)
            : TransparentBackgroundBox.IsChecked == true && _selected.RemoveBackground
                ? ImageProcessingService.CreateTransparentSquare(_bitmap, _selected.Bounds, 220,
                    (int)TransparencyToleranceSlider.Value, (int)TransparencyFeatherSlider.Value,
                    (TransparencyBackgroundSource)Math.Clamp(TransparencyBackgroundSourceBox.SelectedIndex, 0, 2),
                    _sourcePath is not null && _manualBackgroundColors.TryGetValue(_sourcePath, out Color sampled) ? sampled : null)
                : ImageProcessingService.CreateSquare(_bitmap, _selected.Bounds, 220, AutoBackgroundBox.IsChecked == true, Colors.White); }
        catch { OutputPreview.Source = null; }
    }

    private void TaskList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TaskList.SelectedItem is not ImageTask task) return;
        _suppressWorkModeChange = true;
        WorkModeBox.SelectedIndex = _longModeStates.TryGetValue(task.Path, out bool isLong) && isLong ? 1 : 0;
        _suppressWorkModeChange = false;
        UpdateWorkModeUi();
        LoadFile(task.Path);
    }

    private void ConfirmTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is not ImageTask task) return;
        task.Status = "已确认"; TaskList.Items.Refresh();
        StatusText.Text = $"已确认任务：{task.Name}";
        SaveRecoveryProject();
    }

    private void RemoveTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is not ImageTask task) return;
        int index = TaskList.SelectedIndex;
        _tasks.Remove(task); _regionStates.Remove(task.Path); _histories.Remove(task.Path); _longModeStates.Remove(task.Path);
        _manualBackgroundColors.Remove(task.Path); TaskList.Items.Remove(task);
        if (_tasks.Count == 0)
        {
            _bitmap = null; _sourcePath = null; _regions = []; _selected = null;
            SourceImage.Source = null; Overlay.Children.Clear(); OutputPreview.Source = null;
            EmptyState.Visibility = Visibility.Visible;
            ImageInfoText.Text = string.Empty; StatusText.Text = "请添加图片任务";
        }
        else TaskList.SelectedIndex = Math.Min(index, _tasks.Count - 1);
    }

    private void BoundsBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _bitmap is null) return;
        if (IsLongMode)
        {
            if (!TryNumber(HeightBox.Text, out double requestedHeight)) return;
            int index = _regions.IndexOf(_selected);
            if (index < 0 || _regions.Count < 2) return;
            PushHistory();
            if (index < _regions.Count - 1)
            {
                var next = _regions[index + 1];
                double combined = _selected.Bounds.Height + next.Bounds.Height;
                double height = Math.Round(Math.Clamp(requestedHeight, 20, combined - 20));
                _selected.Bounds = new Rect(0, _selected.Bounds.Y, _bitmap.PixelWidth, height);
                next.Bounds = new Rect(0, _selected.Bounds.Bottom, _bitmap.PixelWidth, combined - height);
            }
            else
            {
                var previous = _regions[index - 1];
                double combined = previous.Bounds.Height + _selected.Bounds.Height;
                double height = Math.Round(Math.Clamp(requestedHeight, 20, combined - 20));
                previous.Bounds = new Rect(0, previous.Bounds.Y, _bitmap.PixelWidth, combined - height);
                _selected.Bounds = new Rect(0, previous.Bounds.Bottom, _bitmap.PixelWidth, height);
            }
            RenderRegions(); return;
        }
        if (TryNumber(XBox.Text, out double x) && TryNumber(YBox.Text, out double y) &&
            TryNumber(WidthBox.Text, out double w) && TryNumber(HeightBox.Text, out double h))
        {
            PushHistory();
            w = Math.Clamp(w, 20, _bitmap.PixelWidth); h = Math.Clamp(h, 20, _bitmap.PixelHeight);
            x = Math.Clamp(x, 0, _bitmap.PixelWidth - w); y = Math.Clamp(y, 0, _bitmap.PixelHeight - h);
            _selected.Bounds = new Rect(x, y, w, h); RenderRegions();
        }
        static bool TryNumber(string text, out double value) => double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateBoundsBoxes()
    {
        if (_selected is null) { XBox.Text = YBox.Text = WidthBox.Text = HeightBox.Text = string.Empty; return; }
        XBox.Text = Math.Round(_selected.Bounds.X).ToString(CultureInfo.InvariantCulture);
        YBox.Text = Math.Round(_selected.Bounds.Y).ToString(CultureInfo.InvariantCulture);
        WidthBox.Text = Math.Round(_selected.Bounds.Width).ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = Math.Round(_selected.Bounds.Height).ToString(CultureInfo.InvariantCulture);
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (QualityText is not null) QualityText.Text = ((int)e.NewValue).ToString(); }

    private void NamingRule_Changed(object sender, RoutedEventArgs e) => UpdateNamingExample();

    private void UpdateNamingExample()
    {
        if (NamingModeBox is null || NamingPrefixBox is null || NamingSuffixBox is null ||
            NamingStartNumberBox is null || NamingDigitsBox is null || NamingExampleText is null || FormatBox is null) return;
        NamingPrefixBox.IsEnabled = NamingModeBox.SelectedIndex == 1;
        int start = int.TryParse(NamingStartNumberBox.Text, out int parsedStart) ? Math.Max(0, parsedStart) : 1;
        int digits = NamingDigitsBox.SelectedItem is ComboBoxItem digitsItem &&
                     int.TryParse(digitsItem.Tag?.ToString(), out int parsedDigits) ? parsedDigits : 2;
        string format = TransparentBackgroundBox?.IsChecked == true
            ? "PNG"
            : (FormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PNG";
        string extension = format == "JPG" ? ".jpg" : ".png";
        string example = BuildExportFileName("商品图", false, 0, extension, NamingModeBox.SelectedIndex,
            NamingPrefixBox.Text, NamingSuffixBox.Text, start, digits);
        NamingExampleText.Text = $"示例：{example}";
    }

    private void DetectionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SensitivityText is not null) SensitivityText.Text = ((int)SensitivitySlider.Value).ToString();
        if (GroupingText is not null) GroupingText.Text = ((int)GroupingSlider.Value).ToString();
    }

    private void TransparentBackgroundBox_Click(object sender, RoutedEventArgs e)
    {
        if (TransparencyOptionsPanel is null) return;
        bool enabled = !IsLongMode && TransparentBackgroundBox.IsChecked == true;
        TransparencyOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (enabled) FormatBox.SelectedIndex = 0;
        FormatBox.IsEnabled = !enabled;
        AutoBackgroundBox.IsEnabled = !IsLongMode && !enabled;
        UpdateNamingExample();
        if (_bitmap is not null) UpdateOutputPreview();
    }

    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // ValueChanged can fire while InitializeComponent is still constructing the
        // second slider. Do not read sibling controls until the whole group exists.
        if (TransparencyToleranceText is null || TransparencyFeatherText is null ||
            TransparencyToleranceSlider is null || TransparencyFeatherSlider is null) return;
        TransparencyToleranceText.Text = ((int)TransparencyToleranceSlider.Value).ToString();
        TransparencyFeatherText.Text = ((int)TransparencyFeatherSlider.Value).ToString();
        if (_bitmap is not null && TransparentBackgroundBox.IsChecked == true) UpdateOutputPreview();
    }

    private void TransparencyBackgroundSourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_bitmap is not null && TransparentBackgroundBox?.IsChecked == true)
            UpdateOutputPreview();
    }

    private void TransparencyStrengthBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TransparencyToleranceSlider is null || TransparencyFeatherSlider is null || TransparencyStrengthBox is null) return;
        (double tolerance, double feather) = TransparencyStrengthBox.SelectedIndex switch
        {
            0 => (5, 10),
            2 => (20, 24),
            _ => (10, 16)
        };
        TransparencyToleranceSlider.Value = tolerance;
        TransparencyFeatherSlider.Value = feather;
    }

    private void PickBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmap is null || _sourcePath is null) return;
        _isPickingBackgroundColor = true;
        Overlay.Cursor = Cursors.Cross;
        StatusText.Text = "请在原图的背景区域单击取色，按 Esc 可取消";
    }

    private void ClearSampledBackground_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is null) return;
        _manualBackgroundColors.Remove(_sourcePath);
        _isPickingBackgroundColor = false;
        Overlay.Cursor = Cursors.Arrow;
        UpdateSampledBackgroundUi();
        UpdateOutputPreview();
    }

    private void UpdateSampledBackgroundUi()
    {
        if (SampledBackgroundSwatch is null || SampledBackgroundText is null) return;
        if (_sourcePath is not null && _manualBackgroundColors.TryGetValue(_sourcePath, out Color sampled))
        {
            SampledBackgroundSwatch.Background = new SolidColorBrush(sampled);
            SampledBackgroundText.Text = $"已取样：{sampled}";
        }
        else
        {
            SampledBackgroundSwatch.Background = Brushes.Transparent;
            SampledBackgroundText.Text = "当前使用自动采样";
        }
    }

    private void RemoveBackgroundForRegionBox_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressRegionBackgroundChange || _selected is null) return;
        PushHistory();
        _selected.RemoveBackground = RemoveBackgroundForRegionBox.IsChecked == true;
        RenderRegions();
    }

    private async void CheckTransparency_Click(object sender, RoutedEventArgs e)
    {
        if (_tasks.Count == 0 || TransparentBackgroundBox.IsChecked != true)
        {
            ShowMessage( "请先添加图片并启用透明 PNG 导出。", "透明结果检查");
            return;
        }
        int tolerance = (int)TransparencyToleranceSlider.Value;
        int feather = (int)TransparencyFeatherSlider.Value;
        var sourceMode = (TransparencyBackgroundSource)Math.Clamp(TransparencyBackgroundSourceBox.SelectedIndex, 0, 2);
        DetectionOptions detection = ReadDetectionOptions();
        var checks = _tasks.Select(task => new
        {
            task.Path,
            task.Name,
            IsLong = _longModeStates.TryGetValue(task.Path, out bool isLong) && isLong,
            Regions = _regionStates.TryGetValue(task.Path, out var regions)
                ? regions.Select(r => new CropRegion { Number = r.Number, Bounds = r.Bounds, RemoveBackground = r.RemoveBackground }).ToList()
                : null,
            Sampled = _manualBackgroundColors.TryGetValue(task.Path, out Color sampled) ? sampled : (Color?)null
        }).ToList();
        try
        {
            IsEnabled = false;
            var warnings = await Task.Run(() =>
            {
                var result = new List<string>();
                foreach (var task in checks.Where(t => !t.IsLong))
                {
                    var bitmap = ImageProcessingService.LoadImage(task.Path);
                    var regions = task.Regions ?? DetectUsingOptions(bitmap, detection).ToList();
                    foreach (var region in regions.Where(r => r.RemoveBackground))
                    {
                        var preview = ImageProcessingService.CreateTransparentSquare(bitmap, region.Bounds, 220,
                            tolerance, feather, sourceMode, task.Sampled);
                        var stats = ImageProcessingService.AnalyzeAlpha(preview);
                        string? issue = stats.Clear < .04 ? "几乎没有移除背景"
                            : stats.Clear > .94 ? "透明区域过多"
                            : stats.Partial > .22 ? "半透明边缘较多"
                            : null;
                        if (issue is not null)
                            result.Add($"{task.Name} / 切片 {region.Number}：{issue}（透明 {stats.Clear:P0}）");
                    }
                }
                return result;
            });
            string message = warnings.Count == 0
                ? "检查完成，没有发现明显异常。"
                : $"发现 {warnings.Count} 个建议复查的切片：\n\n" + string.Join("\n", warnings.Take(12)) +
                  (warnings.Count > 12 ? $"\n……另有 {warnings.Count - 12} 项" : string.Empty);
            ShowMessage( message, "透明结果检查", MessageBoxButton.OK,
                warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ShowMessage( $"检查失败：\n{ex.Message}", "透明结果检查", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsEnabled = true; }
    }

    private DetectionOptions ReadDetectionOptions()
    {
        double minArea = double.TryParse(MinimumAreaBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var area) ? area : .06;
        int padding = int.TryParse(ObjectPaddingBox.Text, out var pad) ? pad : 18;
        return new DetectionOptions(DetectionModeBox.SelectedIndex, SensitivitySlider.Value,
            (int)GroupingSlider.Value, Math.Clamp(minArea, .001, 10), Math.Clamp(padding, 0, 300));
    }

    private static IReadOnlyList<CropRegion> DetectUsingOptions(BitmapSource bitmap, DetectionOptions options)
    {
        if (options.Mode == 1) return ImageProcessingService.DetectCards(bitmap);
        if (options.Mode == 2) return ImageProcessingService.DetectObjects(bitmap, options.Sensitivity,
            options.Grouping, options.MinimumArea, options.Padding);
        var cards = ImageProcessingService.DetectCards(bitmap);
        return cards.Count >= 2 ? cards : ImageProcessingService.DetectObjects(bitmap, options.Sensitivity,
            options.Grouping, options.MinimumArea, options.Padding);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) AddFiles(files);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isPickingBackgroundColor)
        {
            _isPickingBackgroundColor = false; Overlay.Cursor = Cursors.Arrow;
            StatusText.Text = "已取消背景取色"; e.Handled = true;
        }
        else if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Redo(); e.Handled = true; }
    }

    private EditHistory? CurrentHistory()
    {
        if (_sourcePath is null) return null;
        if (!_histories.TryGetValue(_sourcePath, out var history))
            _histories[_sourcePath] = history = new EditHistory();
        return history;
    }

    private List<RegionSnapshot> Snapshot() => _regions
        .Select(r => new RegionSnapshot(r.Bounds, r.RemoveBackground)).ToList();
    private void PushHistory()
    {
        var history = CurrentHistory();
        if (history is null) return;
        history.Undo.Push(Snapshot()); history.Redo.Clear();
    }

    private void ApplySnapshot(List<RegionSnapshot> snapshot)
    {
        _regions = snapshot.Select((r, i) => new CropRegion
            { Number = i + 1, Bounds = r.Bounds, RemoveBackground = r.RemoveBackground }).ToList();
        if (_sourcePath is not null) _regionStates[_sourcePath] = _regions;
        _selected = _regions.FirstOrDefault(); RenderRegions();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();
    private void Undo()
    {
        var history = CurrentHistory();
        if (history is null || history.Undo.Count == 0) { StatusText.Text = "没有可撤销的操作"; return; }
        history.Redo.Push(Snapshot()); ApplySnapshot(history.Undo.Pop()); StatusText.Text = "已撤销上一步操作";
    }
    private void Redo()
    {
        var history = CurrentHistory();
        if (history is null || history.Redo.Count == 0) { StatusText.Text = "没有可重做的操作"; return; }
        history.Undo.Push(Snapshot()); ApplySnapshot(history.Redo.Pop()); StatusText.Text = "已重做操作";
    }

    private void InferGrid_Click(object sender, RoutedEventArgs e) => InferGridParameters(true);
    private void InferGridParameters(bool showStatus)
    {
        if (_bitmap is null || _regions.Count == 0) return;
        double medianWidth = Median(_regions.Select(r => r.Bounds.Width));
        double medianHeight = Median(_regions.Select(r => r.Bounds.Height));
        var columns = Cluster(_regions, r => r.Bounds.Left + r.Bounds.Width / 2, medianWidth * .45);
        var rows = Cluster(_regions, r => r.Bounds.Top + r.Bounds.Height / 2, medianHeight * .45);
        var columnRects = columns.Select(g => new Rect(g.Average(r => r.Bounds.Left), 0,
            g.Average(r => r.Bounds.Width), 1)).OrderBy(r => r.Left).ToList();
        var rowRects = rows.Select(g => new Rect(0, g.Average(r => r.Bounds.Top),
            1, g.Average(r => r.Bounds.Height))).OrderBy(r => r.Top).ToList();
        double marginX = (columnRects[0].Left + (_bitmap.PixelWidth - columnRects[^1].Right)) / 2;
        double marginY = (rowRects[0].Top + (_bitmap.PixelHeight - rowRects[^1].Bottom)) / 2;
        double gapX = columnRects.Count > 1 ? Median(columnRects.Zip(columnRects.Skip(1), (a, b) => b.Left - a.Right)) : 0;
        double gapY = rowRects.Count > 1 ? Median(rowRects.Zip(rowRects.Skip(1), (a, b) => b.Top - a.Bottom)) : 0;
        RowsBox.Text = rows.Count.ToString(); ColumnsBox.Text = columns.Count.ToString();
        MarginXBox.Text = FormatNumber(marginX); MarginYBox.Text = FormatNumber(marginY);
        GapXBox.Text = FormatNumber(gapX); GapYBox.Text = FormatNumber(gapY);
        if (showStatus) StatusText.Text = $"已推算：{columns.Count} 列 × {rows.Count} 行，边距 {FormatNumber(marginX)}/{FormatNumber(marginY)}，间距 {FormatNumber(gapX)}/{FormatNumber(gapY)} px";

        static List<List<CropRegion>> Cluster(IEnumerable<CropRegion> source, Func<CropRegion, double> key, double tolerance)
        {
            var groups = new List<List<CropRegion>>();
            foreach (var region in source.OrderBy(key))
            {
                var group = groups.LastOrDefault();
                if (group is null || Math.Abs(key(region) - group.Average(key)) > tolerance) groups.Add([region]);
                else group.Add(region);
            }
            return groups;
        }
        static double Median(IEnumerable<double> source) { var values = source.Order().ToArray(); return values.Length == 0 ? 0 : values[values.Length / 2]; }
        static string FormatNumber(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_bitmap is null) return;
        SetZoom(_zoom * (e.Delta > 0 ? 1.12 : 1 / 1.12), e.GetPosition(ImageScrollViewer));
        e.Handled = true;
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.2);
    private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetZoom(1);
    private void SetZoom(double value, Point? anchor = null)
    {
        double old = _zoom; _zoom = Math.Clamp(value, .15, 5);
        Point point = anchor ?? new Point(ImageScrollViewer.ViewportWidth / 2, ImageScrollViewer.ViewportHeight / 2);
        double oldX = ImageScrollViewer.HorizontalOffset, oldY = ImageScrollViewer.VerticalOffset;
        CanvasScale.ScaleX = CanvasScale.ScaleY = _zoom;
        ImageHost.UpdateLayout();
        double ratio = _zoom / old;
        ImageScrollViewer.ScrollToHorizontalOffset((oldX + point.X) * ratio - point.X);
        ImageScrollViewer.ScrollToVerticalOffset((oldY + point.Y) * ratio - point.Y);
        ZoomTextButton.Content = $"{_zoom:P0}";
    }

    private static string SettingsPath => System.IO.Path.Combine(LocalDataDirectory, "settings.json");
    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (settings is null) return;
            SizeBox.SelectedIndex = Math.Clamp(settings.OutputSizeIndex, 0, SizeBox.Items.Count - 1);
            FormatBox.SelectedIndex = Math.Clamp(settings.FormatIndex, 0, FormatBox.Items.Count - 1);
            ConflictPolicyBox.SelectedIndex = Math.Clamp(settings.ConflictPolicyIndex, 0, ConflictPolicyBox.Items.Count - 1);
            QualitySlider.Value = settings.JpegQuality; AutoBackgroundBox.IsChecked = settings.AutoBackground;
            RowsBox.Text = settings.Rows; ColumnsBox.Text = settings.Columns;
            MarginXBox.Text = settings.MarginX; MarginYBox.Text = settings.MarginY;
            GapXBox.Text = settings.GapX; GapYBox.Text = settings.GapY;
            DetectionModeBox.SelectedIndex = Math.Clamp(settings.DetectionModeIndex, 0, 2);
            SensitivitySlider.Value = settings.DetectionSensitivity;
            GroupingSlider.Value = settings.GroupingDistance;
            MinimumAreaBox.Text = settings.MinimumAreaPercent; ObjectPaddingBox.Text = settings.ObjectPadding;
            TransparentBackgroundBox.IsChecked = settings.TransparentBackground;
            TransparencyBackgroundSourceBox.SelectedIndex = Math.Clamp(settings.TransparencyBackgroundSourceIndex, 0, 2);
            TransparencyStrengthBox.SelectedIndex = Math.Clamp(settings.TransparencyStrengthIndex, 0, 2);
            TransparencyToleranceSlider.Value = settings.TransparencyTolerance;
            TransparencyFeatherSlider.Value = settings.TransparencyFeather;
            NamingModeBox.SelectedIndex = Math.Clamp(settings.NamingModeIndex, 0, 1);
            NamingPrefixBox.Text = settings.NamingPrefix;
            NamingSuffixBox.Text = settings.NamingSuffix;
            NamingStartNumberBox.Text = settings.NamingStartNumber;
            NamingDigitsBox.SelectedIndex = Math.Clamp(settings.NamingDigitsIndex, 0, 2);
            CreateSourceSubfolderBox.IsChecked = settings.CreateSourceSubfolder;
            OpenFolderAfterExportBox.IsChecked = settings.OpenFolderAfterExport;
            _autoRestoreSession = settings.AutoRestoreSession;
            _autoSaveIntervalSeconds = Math.Clamp(settings.AutoSaveIntervalSeconds, 5, 300);
            _fontFamilyName = settings.FontFamilyName;
            _languageCode = settings.LanguageCode;
            _suppressWorkModeChange = true;
            WorkModeBox.SelectedIndex = Math.Clamp(settings.WorkModeIndex, 0, 1);
            _suppressWorkModeChange = false;
            LongSplitModeBox.SelectedIndex = Math.Clamp(settings.LongSplitModeIndex, 0, 3);
            LongCountBox.Text = settings.LongSliceCount;
            LongFixedHeightBox.Text = settings.LongFixedHeight;
            LongCustomHeightsBox.Text = settings.LongCustomHeights;
            SmartTargetHeightBox.Text = settings.SmartTargetHeight;
            SmartSearchRangeBox.Text = settings.SmartSearchRange;
            UpdateWorkModeUi();
            TransparentBackgroundBox_Click(this, null!);
            SetZoom(settings.Zoom);
            UpdateNamingExample();
            ApplyFontFamily();
        }
        catch { StatusText.Text = "配置读取失败，已使用默认设置"; }
    }
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath)!);
            var settings = new AppSettings
            {
                OutputSizeIndex = SizeBox.SelectedIndex, FormatIndex = FormatBox.SelectedIndex,
                ConflictPolicyIndex = ConflictPolicyBox.SelectedIndex,
                JpegQuality = QualitySlider.Value, AutoBackground = AutoBackgroundBox.IsChecked == true,
                Rows = RowsBox.Text, Columns = ColumnsBox.Text, MarginX = MarginXBox.Text,
                MarginY = MarginYBox.Text, GapX = GapXBox.Text, GapY = GapYBox.Text, Zoom = _zoom
                , DetectionModeIndex = DetectionModeBox.SelectedIndex,
                DetectionSensitivity = SensitivitySlider.Value, GroupingDistance = GroupingSlider.Value,
                MinimumAreaPercent = MinimumAreaBox.Text, ObjectPadding = ObjectPaddingBox.Text,
                TransparentBackground = TransparentBackgroundBox.IsChecked == true,
                TransparencyBackgroundSourceIndex = TransparencyBackgroundSourceBox.SelectedIndex,
                TransparencyStrengthIndex = TransparencyStrengthBox.SelectedIndex,
                TransparencyTolerance = TransparencyToleranceSlider.Value,
                TransparencyFeather = TransparencyFeatherSlider.Value,
                NamingModeIndex = NamingModeBox.SelectedIndex,
                NamingPrefix = NamingPrefixBox.Text, NamingSuffix = NamingSuffixBox.Text,
                NamingStartNumber = NamingStartNumberBox.Text, NamingDigitsIndex = NamingDigitsBox.SelectedIndex,
                CreateSourceSubfolder = CreateSourceSubfolderBox.IsChecked == true,
                OpenFolderAfterExport = OpenFolderAfterExportBox.IsChecked == true,
                AutoRestoreSession = _autoRestoreSession,
                AutoSaveIntervalSeconds = _autoSaveIntervalSeconds,
                FontFamilyName = _fontFamilyName,
                LanguageCode = _languageCode,
                WorkModeIndex = WorkModeBox.SelectedIndex, LongSplitModeIndex = LongSplitModeBox.SelectedIndex,
                LongSliceCount = LongCountBox.Text, LongFixedHeight = LongFixedHeightBox.Text,
                LongCustomHeights = LongCustomHeightsBox.Text,
                SmartTargetHeight = SmartTargetHeightBox.Text, SmartSearchRange = SmartSearchRangeBox.Text
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void ApplyFontFamily()
    {
        try
        {
            FontFamily = string.IsNullOrWhiteSpace(_fontFamilyName) || _fontFamilyName == "System"
                ? SystemFonts.MessageFontFamily
                : new FontFamily(_fontFamilyName == "Roboto"
                    ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Resources/Roboto/#Roboto"
                    : _fontFamilyName);
        }
        catch
        {
            _fontFamilyName = "System";
            FontFamily = SystemFonts.MessageFontFamily;
        }
    }

    private MessageBoxResult ShowMessage(string message, string caption,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        => MessageBox.Show(this, LocalizationService.Translate(message), LocalizationService.Translate(caption), buttons, image);
}
