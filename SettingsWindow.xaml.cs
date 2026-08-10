using System.Windows;
using CutPic.Models;
using System.Windows.Controls;
using System.Windows.Media;

namespace CutPic;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }

    public SettingsWindow(AppSettings settings)
    {
        Settings = settings;
        InitializeComponent();
        ConflictPolicyBox.SelectedIndex = Math.Clamp(settings.ConflictPolicyIndex, 0, 2);
        QualitySlider.Value = Math.Clamp(settings.JpegQuality, 50, 100);
        CreateSourceSubfolderBox.IsChecked = settings.CreateSourceSubfolder;
        OpenFolderAfterExportBox.IsChecked = settings.OpenFolderAfterExport;
        NamingModeBox.SelectedIndex = Math.Clamp(settings.NamingModeIndex, 0, 1);
        NamingPrefixBox.Text = settings.NamingPrefix;
        NamingSuffixBox.Text = settings.NamingSuffix;
        NamingStartNumberBox.Text = settings.NamingStartNumber;
        NamingDigitsBox.SelectedIndex = Math.Clamp(settings.NamingDigitsIndex, 0, 2);
        AutoRestoreBox.IsChecked = settings.AutoRestoreSession;
        AutoSaveIntervalBox.Text = settings.AutoSaveIntervalSeconds.ToString();
        FontFamilyBox.SelectedIndex = settings.FontFamilyName switch
        {
            "Microsoft YaHei UI" => 1, "Segoe UI" => 2, "Arial" => 3, "Roboto" => 4,
            "HarmonyOS Sans SC" => 5, _ => 0
        };
        UpdateNamingMode();
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityText is not null) QualityText.Text = ((int)e.NewValue).ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(NamingStartNumberBox.Text, out int start) || start < 0)
        {
            MessageBox.Show(this, "起始编号必须是大于或等于 0 的整数。", "设置无效");
            NamingStartNumberBox.Focus(); return;
        }
        if (!int.TryParse(AutoSaveIntervalBox.Text, out int interval) || interval is < 5 or > 300)
        {
            MessageBox.Show(this, "自动保存间隔必须在 5–300 秒之间。", "设置无效");
            AutoSaveIntervalBox.Focus(); return;
        }
        Settings.ConflictPolicyIndex = ConflictPolicyBox.SelectedIndex;
        Settings.JpegQuality = QualitySlider.Value;
        Settings.CreateSourceSubfolder = CreateSourceSubfolderBox.IsChecked == true;
        Settings.OpenFolderAfterExport = OpenFolderAfterExportBox.IsChecked == true;
        Settings.NamingModeIndex = NamingModeBox.SelectedIndex;
        Settings.NamingPrefix = NamingPrefixBox.Text.Trim();
        Settings.NamingSuffix = NamingSuffixBox.Text.Trim();
        Settings.NamingStartNumber = start.ToString();
        Settings.NamingDigitsIndex = NamingDigitsBox.SelectedIndex;
        Settings.AutoRestoreSession = AutoRestoreBox.IsChecked == true;
        Settings.AutoSaveIntervalSeconds = interval;
        Settings.FontFamilyName = (FontFamilyBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void NamingModeBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateNamingMode();

    private void UpdateNamingMode()
    {
        if (NamingPrefixBox is not null && NamingModeBox is not null)
            NamingPrefixBox.IsEnabled = NamingModeBox.SelectedIndex == 1;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ConflictPolicyBox.SelectedIndex = 0;
        QualitySlider.Value = 92;
        CreateSourceSubfolderBox.IsChecked = false;
        OpenFolderAfterExportBox.IsChecked = true;
        NamingModeBox.SelectedIndex = 0;
        NamingPrefixBox.Text = string.Empty;
        NamingSuffixBox.Text = string.Empty;
        NamingStartNumberBox.Text = "1";
        NamingDigitsBox.SelectedIndex = 0;
        AutoRestoreBox.IsChecked = true;
        AutoSaveIntervalBox.Text = "15";
        FontFamilyBox.SelectedIndex = 0;
    }

    private void FontFamilyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontFamilyBox?.SelectedItem is not ComboBoxItem item) return;
        string name = item.Tag?.ToString() ?? "System";
        FontFamily = name == "System" ? SystemFonts.MessageFontFamily : new FontFamily(name == "Roboto"
            ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Resources/Roboto/#Roboto" : name);
    }
}
