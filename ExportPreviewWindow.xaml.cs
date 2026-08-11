using System.Collections.ObjectModel;
using System.Windows;
using CutPic.Models;
using CutPic.Services;

namespace CutPic;

public partial class ExportPreviewWindow : Window
{
    public ObservableCollection<ExportPreviewItem> Items { get; }
    public HashSet<string> SelectedKeys => Items.Where(item => item.IsSelected).Select(item => item.Key).ToHashSet();
    public ExportPreviewWindow(IEnumerable<ExportPreviewItem> items)
    {
        Items = new ObservableCollection<ExportPreviewItem>(items);
        InitializeComponent(); PreviewItems.ItemsSource = Items;
        foreach (var item in Items) item.PropertyChanged += (_, _) => UpdateSummary();
        UpdateSummary(); LocalizationService.Apply(this);
    }
    private void UpdateSummary() => SummaryText.Text = LocalizationService.Translate(
        $"共 {Items.Count} 个切片 · 已选择 {Items.Count(i => i.IsSelected)} 个 · 警告 {Items.Count(i => i.HasWarning)} 个");
    private void SelectAll_Click(object sender, RoutedEventArgs e) { foreach (var item in Items) item.IsSelected = true; }
    private void SelectNone_Click(object sender, RoutedEventArgs e) { foreach (var item in Items) item.IsSelected = false; }
    private void SelectSafe_Click(object sender, RoutedEventArgs e) { foreach (var item in Items) item.IsSelected = !item.HasWarning; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (Items.All(item => !item.IsSelected)) { MessageBox.Show(this, LocalizationService.Translate("请至少选择一个切片。"), LocalizationService.Translate("无法导出")); return; }
        DialogResult = true;
    }
}
