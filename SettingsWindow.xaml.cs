using System.Windows;
using CutPic.Models;
using CutPic.Services;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace CutPic;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; }
    private readonly Dictionary<string, TextBox> _shortcutBoxes = new(StringComparer.OrdinalIgnoreCase);

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
        LanguageBox.SelectedIndex = settings.LanguageCode == "en-US" ? 1 : 0;
        BuildShortcutEditor(ShortcutService.MergeWithDefaults(settings.Shortcuts));
        UpdateNamingMode();
        LocalizationService.Apply(this);
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityText is not null) QualityText.Text = ((int)e.NewValue).ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var shortcuts = _shortcutBoxes.ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.OrdinalIgnoreCase);
        var conflict = shortcuts.GroupBy(pair => ShortcutService.Normalize(pair.Value), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (conflict is not null)
        {
            string names = string.Join("、", conflict.Select(pair => ShortcutService.Definitions.First(item => item.Id == pair.Key).Name));
            ShowMessage($"快捷键 {conflict.Key} 已分配给多个操作：{names}", "快捷键冲突");
            _shortcutBoxes[conflict.First().Key].Focus();
            return;
        }
        if (!int.TryParse(NamingStartNumberBox.Text, out int start) || start < 0)
        {
            ShowMessage( "起始编号必须是大于或等于 0 的整数。", "设置无效");
            NamingStartNumberBox.Focus(); return;
        }
        if (!int.TryParse(AutoSaveIntervalBox.Text, out int interval) || interval is < 5 or > 300)
        {
            ShowMessage( "自动保存间隔必须在 5–300 秒之间。", "设置无效");
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
        Settings.LanguageCode = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "zh-CN";
        Settings.Shortcuts = shortcuts;
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
        LanguageBox.SelectedIndex = 0;
        ResetShortcutEditor();
    }

    private void BuildShortcutEditor(IReadOnlyDictionary<string, string> values)
    {
        ShortcutListPanel.Children.Clear();
        _shortcutBoxes.Clear();
        foreach (ShortcutDefinition definition in ShortcutService.Definitions)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            row.Children.Add(new TextBlock
            {
                Text = LocalizationService.IsEnglish ? definition.EnglishName : definition.Name,
                VerticalAlignment = VerticalAlignment.Center
            });
            var box = new TextBox
            {
                Text = values.TryGetValue(definition.Id, out string? value) ? value : definition.DefaultGesture,
                IsReadOnly = true, Tag = definition.Id, HorizontalContentAlignment = HorizontalAlignment.Center,
                ToolTip = LocalizationService.Translate("点击后按下新的快捷键")
            };
            box.SetResourceReference(StyleProperty, "MaterialDesignOutlinedTextBox");
            box.PreviewKeyDown += ShortcutBox_PreviewKeyDown;
            Grid.SetColumn(box, 1); row.Children.Add(box);
            ShortcutListPanel.Children.Add(row);
            _shortcutBoxes[definition.Id] = box;
        }
    }

    private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (e.Key == Key.Escape) { Keyboard.ClearFocus(); e.Handled = true; return; }
        if (e.Key == Key.Tab) return;
        string gesture = ShortcutService.FromKeyEvent(e);
        if (gesture.Length == 0) { e.Handled = true; return; }
        box.Text = gesture;
        e.Handled = true;
    }

    private void ResetShortcuts_Click(object sender, RoutedEventArgs e) => ResetShortcutEditor();
    private void ResetShortcutEditor() => BuildShortcutEditor(ShortcutService.CreateDefaults());

    private void FontFamilyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontFamilyBox?.SelectedItem is not ComboBoxItem item) return;
        string name = item.Tag?.ToString() ?? "System";
        FontFamily = name == "System" ? SystemFonts.MessageFontFamily : new FontFamily(name == "Roboto"
            ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Resources/Roboto/#Roboto" : name);
    }

    private MessageBoxResult ShowMessage(string message, string caption)
        => MessageBox.Show(this, LocalizationService.Translate(message), LocalizationService.Translate(caption));
}
