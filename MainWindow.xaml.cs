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

namespace CutPic;

public partial class MainWindow : Window
{
    private sealed record ImageTask(string Path, string Name);
    private readonly record struct DetectionOptions(int Mode, double Sensitivity, int Grouping, double MinimumArea, int Padding);
    private sealed class EditHistory
    {
        public Stack<List<Rect>> Undo { get; } = new();
        public Stack<List<Rect>> Redo { get; } = new();
    }
    private List<CropRegion> _regions = [];
    private readonly List<ImageTask> _tasks = [];
    private readonly Dictionary<string, List<CropRegion>> _regionStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EditHistory> _histories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _longModeStates = new(StringComparer.OrdinalIgnoreCase);
    private BitmapSource? _bitmap;
    private string? _sourcePath;
    private CropRegion? _selected;
    private Point _dragStart;
    private Rect _dragOriginal;
    private Rect _dragNextOriginal;
    private DragMode _dragMode;
    private double _zoom = 1;
    private bool _suppressWorkModeChange;
    private bool IsLongMode => WorkModeBox.SelectedIndex == 1;
    private enum DragMode { None, Move, Resize }

    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closing += (_, _) => SaveSettings();
        LoadSettings();
        LongSplitModeBox_SelectionChanged(this, null!);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*", Multiselect = true };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
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
        catch (Exception ex) { MessageBox.Show(this, $"无法打开图片：\n{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Error); }
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
                        Child = new TextBlock { Text = region.Number.ToString(), Foreground = Brushes.White, FontWeight = FontWeights.Bold } },
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
        if (_tasks.Count == 0) { MessageBox.Show(this, "请先添加图片并确认切割框。", "无法导出"); return; }
        var dialog = new OpenFolderDialog { Title = "选择导出文件夹" };
        if (dialog.ShowDialog() != true) return;
        string format = ((ComboBoxItem)FormatBox.SelectedItem).Content?.ToString() ?? "PNG";
        string extension = format == "JPG" ? ".jpg" : ".png";
        int outputSize = int.TryParse(((ComboBoxItem)SizeBox.SelectedItem).Tag?.ToString(), out int parsed) ? parsed : 512;
        bool autoBackground = AutoBackgroundBox.IsChecked == true;
        int jpegQuality = (int)QualitySlider.Value;
        DetectionOptions detectionOptions = ReadDetectionOptions();
        int longSplitMode = LongSplitModeBox.SelectedIndex;
        int longCount = int.TryParse(LongCountBox.Text, out var parsedCount) ? parsedCount : 3;
        int longFixedHeight = int.TryParse(LongFixedHeightBox.Text, out var parsedHeight) ? parsedHeight : 1200;
        int[] longCustomHeights = ParseHeightList(LongCustomHeightsBox.Text).ToArray();
        try
        {
            IsEnabled = false;
            int total = 0;
            await Task.Run(() =>
            {
                foreach (var task in _tasks)
                {
                    var bitmap = ImageProcessingService.LoadImage(task.Path);
                    bool isLongTask = _longModeStates.TryGetValue(task.Path, out bool taskMode) && taskMode;
                    var regions = _regionStates.TryGetValue(task.Path, out var saved)
                        ? saved
                        : isLongTask
                            ? BuildLongRegions(bitmap.PixelWidth, bitmap.PixelHeight, longSplitMode, longCount, longFixedHeight, longCustomHeights)
                            : DetectUsingOptions(bitmap, detectionOptions).ToList();
                    string baseName = System.IO.Path.GetFileNameWithoutExtension(task.Path);
                    for (int i = 0; i < regions.Count; i++)
                    {
                        var output = isLongTask
                            ? ImageProcessingService.CreateCrop(bitmap, regions[i].Bounds)
                            : ImageProcessingService.CreateSquare(bitmap, regions[i].Bounds, outputSize, autoBackground, Colors.White);
                        string suffix = isLongTask ? $"_part_{i + 1:00}" : $"_{i + 1:00}";
                        string path = System.IO.Path.Combine(dialog.FolderName, $"{baseName}{suffix}{extension}");
                        ImageProcessingService.Save(output, path, format, jpegQuality);
                        total++;
                    }
                }
            });
            StatusText.Text = $"批量导出完成：{_tasks.Count} 个任务，共 {total} 张商品图";
            MessageBox.Show(this, $"已从 {_tasks.Count} 张宫格图中导出 {total} 张正方形商品图。", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, $"导出失败：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsEnabled = true; }
    }

    private void GenerateGrid_Click(object sender, RoutedEventArgs e)
    {
        if (_bitmap is null) return;
        if (!int.TryParse(RowsBox.Text, out int rows) || !int.TryParse(ColumnsBox.Text, out int columns) ||
            !double.TryParse(MarginXBox.Text, out double marginX) || !double.TryParse(MarginYBox.Text, out double marginY) ||
            !double.TryParse(GapXBox.Text, out double gapX) || !double.TryParse(GapYBox.Text, out double gapY) ||
            rows is < 1 or > 20 || columns is < 1 or > 20)
        { MessageBox.Show(this, "请输入有效的行列、边距和间距。行列范围为 1–20。", "参数无效"); return; }
        double cellWidth = (_bitmap.PixelWidth - marginX * 2 - gapX * (columns - 1)) / columns;
        double cellHeight = (_bitmap.PixelHeight - marginY * 2 - gapY * (rows - 1)) / rows;
        if (cellWidth < 20 || cellHeight < 20) { MessageBox.Show(this, "边距或间距过大，无法生成有效选框。", "参数无效"); return; }
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
        CleanCornersBox.IsEnabled = false;
        XBox.IsEnabled = !IsLongMode; YBox.IsEnabled = !IsLongMode; WidthBox.IsEnabled = !IsLongMode;
    }

    private void LongSplitModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LongCountBox is null) return;
        LongCountBox.Visibility = LongSplitModeBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        LongFixedHeightBox.Visibility = LongSplitModeBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        LongCustomHeightsBox.Visibility = LongSplitModeBox.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
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
            var generated = BuildLongRegions(_bitmap.PixelWidth, _bitmap.PixelHeight, mode, count, fixedHeight, custom);
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
            MessageBox.Show(this, ex.Message, "长图切分参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        else
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

    private void RemoveTask_Click(object sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is not ImageTask task) return;
        int index = TaskList.SelectedIndex;
        _tasks.Remove(task); _regionStates.Remove(task.Path); _histories.Remove(task.Path); _longModeStates.Remove(task.Path); TaskList.Items.Remove(task);
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

    private void DetectionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SensitivityText is not null) SensitivityText.Text = ((int)SensitivitySlider.Value).ToString();
        if (GroupingText is not null) GroupingText.Text = ((int)GroupingSlider.Value).ToString();
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
        if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
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

    private List<Rect> Snapshot() => _regions.Select(r => r.Bounds).ToList();
    private void PushHistory()
    {
        var history = CurrentHistory();
        if (history is null) return;
        history.Undo.Push(Snapshot()); history.Redo.Clear();
    }

    private void ApplySnapshot(List<Rect> snapshot)
    {
        _regions = snapshot.Select((r, i) => new CropRegion { Number = i + 1, Bounds = r }).ToList();
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

    private static string SettingsPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CutPic", "settings.json");
    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (settings is null) return;
            SizeBox.SelectedIndex = Math.Clamp(settings.OutputSizeIndex, 0, SizeBox.Items.Count - 1);
            FormatBox.SelectedIndex = Math.Clamp(settings.FormatIndex, 0, FormatBox.Items.Count - 1);
            QualitySlider.Value = settings.JpegQuality; AutoBackgroundBox.IsChecked = settings.AutoBackground;
            RowsBox.Text = settings.Rows; ColumnsBox.Text = settings.Columns;
            MarginXBox.Text = settings.MarginX; MarginYBox.Text = settings.MarginY;
            GapXBox.Text = settings.GapX; GapYBox.Text = settings.GapY;
            DetectionModeBox.SelectedIndex = Math.Clamp(settings.DetectionModeIndex, 0, 2);
            SensitivitySlider.Value = settings.DetectionSensitivity;
            GroupingSlider.Value = settings.GroupingDistance;
            MinimumAreaBox.Text = settings.MinimumAreaPercent; ObjectPaddingBox.Text = settings.ObjectPadding;
            _suppressWorkModeChange = true;
            WorkModeBox.SelectedIndex = Math.Clamp(settings.WorkModeIndex, 0, 1);
            _suppressWorkModeChange = false;
            LongSplitModeBox.SelectedIndex = Math.Clamp(settings.LongSplitModeIndex, 0, 2);
            LongCountBox.Text = settings.LongSliceCount;
            LongFixedHeightBox.Text = settings.LongFixedHeight;
            LongCustomHeightsBox.Text = settings.LongCustomHeights;
            UpdateWorkModeUi();
            SetZoom(settings.Zoom);
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
                JpegQuality = QualitySlider.Value, AutoBackground = AutoBackgroundBox.IsChecked == true,
                Rows = RowsBox.Text, Columns = ColumnsBox.Text, MarginX = MarginXBox.Text,
                MarginY = MarginYBox.Text, GapX = GapXBox.Text, GapY = GapYBox.Text, Zoom = _zoom
                , DetectionModeIndex = DetectionModeBox.SelectedIndex,
                DetectionSensitivity = SensitivitySlider.Value, GroupingDistance = GroupingSlider.Value,
                MinimumAreaPercent = MinimumAreaBox.Text, ObjectPadding = ObjectPaddingBox.Text,
                WorkModeIndex = WorkModeBox.SelectedIndex, LongSplitModeIndex = LongSplitModeBox.SelectedIndex,
                LongSliceCount = LongCountBox.Text, LongFixedHeight = LongFixedHeightBox.Text,
                LongCustomHeights = LongCustomHeightsBox.Text
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
