using System.Windows.Input;

namespace CutPic.Services;

public sealed record ShortcutDefinition(string Id, string Name, string EnglishName, string DefaultGesture);

public static class ShortcutService
{
    public static readonly IReadOnlyList<ShortcutDefinition> Definitions =
    [
        new("OpenImages", "添加图片", "Add Images", "Ctrl+O"),
        new("ImportFolder", "导入文件夹", "Import Folder", "Ctrl+Shift+O"),
        new("SaveProject", "保存项目", "Save Project", "Ctrl+S"),
        new("AutoDetect", "自动识别", "Auto Detect", "F5"),
        new("AddRegion", "新增选框", "Add Region", "A"),
        new("DeleteRegion", "删除选框", "Delete Region", "Delete"),
        new("Undo", "撤销", "Undo", "Ctrl+Z"),
        new("Redo", "重做", "Redo", "Ctrl+Y"),
        new("ConfirmTask", "确认当前任务", "Confirm Current Task", "Ctrl+Enter"),
        new("NextIssue", "下一异常", "Next Issue", "F4"),
        new("ZoomIn", "放大", "Zoom In", "+"),
        new("ZoomOut", "缩小", "Zoom Out", "-"),
        new("ZoomReset", "恢复 100%", "Reset to 100%", "0"),
        new("FitWindow", "适应窗口", "Fit to Window", "Ctrl+0"),
        new("FitWidth", "适应宽度", "Fit to Width", "Ctrl+Shift+0"),
        new("ExportAll", "导出全部", "Export All", "Ctrl+E"),
        new("Settings", "打开设置", "Open Settings", "Ctrl+,")
    ];

    public static Dictionary<string, string> CreateDefaults() =>
        Definitions.ToDictionary(item => item.Id, item => item.DefaultGesture, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string> MergeWithDefaults(IReadOnlyDictionary<string, string>? values)
    {
        var result = CreateDefaults();
        if (values is not null)
            foreach (var pair in values)
                if (result.ContainsKey(pair.Key) && TryParse(pair.Value, out _, out _)) result[pair.Key] = Normalize(pair.Value);
        return result;
    }

    public static bool Matches(KeyEventArgs e, string gesture)
    {
        if (!TryParse(gesture, out Key key, out ModifierKeys modifiers)) return false;
        Key pressed = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.OemPlus && pressed is Key.Add) pressed = Key.OemPlus;
        if (key == Key.OemMinus && pressed is Key.Subtract) pressed = Key.OemMinus;
        if (key == Key.D0 && pressed is Key.NumPad0) pressed = Key.D0;
        if (pressed != key) return false;
        // On common keyboard layouts '+' is Shift+OemPlus. Treat that physical Shift as
        // part of typing the symbol when the configured gesture is simply '+'.
        return Keyboard.Modifiers == modifiers ||
               key == Key.OemPlus && modifiers == ModifierKeys.None && Keyboard.Modifiers == ModifierKeys.Shift;
    }

    public static string FromKeyEvent(KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return string.Empty;
        var parts = new List<string>();
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(KeyName(key));
        return string.Join('+', parts);
    }

    public static string Normalize(string gesture) => TryParse(gesture, out Key key, out ModifierKeys modifiers)
        ? string.Join('+', ModifierNames(modifiers).Append(KeyName(key))) : gesture.Trim();

    private static bool TryParse(string? gesture, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None; modifiers = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(gesture)) return false;
        string trimmed = gesture.Trim();
        if (trimmed.EndsWith("++", StringComparison.Ordinal))
        {
            key = Key.OemPlus;
            foreach (string part in trimmed[..^2].Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
                else return false;
            }
            return true;
        }
        string[] parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        // A lone '+' is a valid shortcut and otherwise disappears when split.
        if (gesture.Trim() == "+") parts = ["+"];
        foreach (string part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
            else if (!TryParseKey(part, out key)) return false;
        }
        return key != Key.None;
    }

    private static bool TryParseKey(string value, out Key key)
    {
        key = value switch { "+" => Key.OemPlus, "-" => Key.OemMinus, "0" => Key.D0, "," => Key.OemComma, _ => Key.None };
        return key != Key.None || Enum.TryParse(value, true, out key);
    }

    private static IEnumerable<string> ModifierNames(ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(ModifierKeys.Shift)) yield return "Shift";
        if (modifiers.HasFlag(ModifierKeys.Alt)) yield return "Alt";
    }

    private static string KeyName(Key key) => key switch
    {
        Key.OemPlus or Key.Add => "+", Key.OemMinus or Key.Subtract => "-", Key.D0 or Key.NumPad0 => "0",
        Key.OemComma => ",", Key.Return => "Enter", _ => key.ToString()
    };
}
