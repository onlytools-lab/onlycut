using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace CutPic.Services;

public static class LocalizationService
{
    public static string LanguageCode { get; private set; } = "zh-CN";
    public static bool IsEnglish => LanguageCode == "en-US";

    private static readonly Dictionary<string, string> Map = new()
    {
        ["CutPic · 智能切图"]="CutPic · Smart Image Slicer", ["智能宫格与素材切图"]="Smart grid and asset slicing",
        ["添加图片"]="Add Images", ["导入整个文件夹"]="Import Folder", ["打开 CutPic 项目"]="Open CutPic Project", ["保存 CutPic 项目"]="Save CutPic Project",
        ["按当前参数重新识别"]="Detect with Current Settings", ["自动识别"]="Auto Detect", ["添加选框"]="Add Region", ["删除选框"]="Delete Region",
        ["撤销"]="Undo", ["重做"]="Redo", ["标记当前任务已确认"]="Mark Current Task Confirmed", ["应用设置"]="Settings", ["导出全部"]="Export All",
        ["图片任务"]="Image Tasks", ["添加任务"]="Add Task", ["移除任务"]="Remove Task", ["支持拖入多张图片"]="Drop multiple images here",
        ["缩小"]="Zoom Out", ["放大"]="Zoom In", ["拖入图片开始切图"]="Drop an image to start", ["工作模式"]="Work Mode", ["智能元素切图"]="Smart Element Slicing",
        ["长图纵向切分"]="Long Image Slicing", ["成品预览"]="Output Preview", ["识别设置"]="Detection Settings", ["自动选择模式"]="Auto Select Mode",
        ["有边界卡片"]="Bordered Cards", ["无边界元素"]="Borderless Elements", ["颜色灵敏度"]="Color Sensitivity", ["碎片合并距离"]="Fragment Merge Distance",
        ["当前工作流"]="Current Workflow", ["识别模式"]="Detection Mode", ["切分方式"]="Split Method", ["输出尺寸"]="Output Size", ["自定义高度（每行一个）"]="Custom Heights (one per line)",
        ["最小面积 %"]="Minimum Area %", ["留白 px"]="Padding px", ["应用识别参数"]="Apply Detection Settings", ["长图切分"]="Long Image Slicing",
        ["按数量平均切分"]="Split Evenly by Count", ["按固定高度切分"]="Split by Fixed Height", ["自定义每段高度"]="Custom Segment Heights", ["按空白区域智能切分"]="Smart Split at Blank Areas",
        ["平均切片数量"]="Number of Slices", ["固定高度 px"]="Fixed Height px", ["期望高度 px"]="Target Height px", ["搜索范围 px"]="Search Range px",
        ["最后一段始终使用剩余高度；切片之间无间距，宽度始终为原图 100%。"]="The final segment uses the remaining height. Slices have no gaps and always keep 100% image width.",
        ["生成长图切片"]="Generate Long-image Slices", ["输出设置"]="Output Settings", ["保持最大边尺寸"]="Keep Original Maximum Side", ["文件格式"]="File Format",
        ["自动采样卡片底色"]="Auto-sample Card Background", ["移除背景并导出透明 PNG"]="Remove Background and Export Transparent PNG", ["去背景来源"]="Background Source",
        ["自动判断"]="Auto", ["无边界背景"]="Borderless Background", ["卡片背景"]="Card Background", ["去背景强度"]="Removal Strength", ["保守"]="Conservative", ["标准"]="Standard", ["强力"]="Strong",
        ["吸取背景色"]="Pick Background Color", ["恢复自动采样"]="Restore Auto Sampling", ["当前使用自动采样"]="Using auto sampling", ["当前切片移除背景"]="Remove Background for Current Slice",
        ["检查透明结果"]="Check Transparency", ["去背景容差"]="Removal Tolerance", ["边缘羽化"]="Edge Feather", ["仅移除与裁切边缘连通的背景，避免主体内部白色区域被挖空。"]="Only removes background connected to crop edges, protecting white areas inside the subject.",
        ["自动清理圆角背景"]="Clean Rounded-corner Background", ["示例："]="Example: ", ["修改命名与导出默认值"]="Edit Naming and Export Defaults",
        ["固定宫格"]="Fixed Grid", ["行数"]="Rows", ["列数"]="Columns", ["横向边距"]="Horizontal Margin", ["纵向边距"]="Vertical Margin", ["横向间距"]="Horizontal Gap", ["纵向间距"]="Vertical Gap",
        ["按参数生成选框"]="Generate Regions", ["从识别结果推算"]="Infer from Detection", ["当前选框"]="Current Region", ["宽度"]="Width", ["高度"]="Height",
        ["拖动选框移动 · 右下角缩放 · 滚轮缩放画布 · Delete 删除 · Ctrl+Z 撤销"]="Drag to move · Resize at bottom-right · Wheel to zoom · Delete to remove · Ctrl+Z to undo",
        ["请添加或拖入图片"]="Add or drop images", ["CutPic 设置"]="CutPic Settings", ["管理不需要频繁调整的默认选项"]="Manage defaults that rarely need adjustment",
        ["导出默认值"]="Export Defaults", ["文件重名处理"]="File Conflict Policy", ["自动追加序号"]="Append Number", ["覆盖已有文件"]="Overwrite Existing", ["跳过已有文件"]="Skip Existing",
        ["JPG 默认质量"]="Default JPG Quality", ["按源图片创建子文件夹"]="Create Folder per Source Image", ["导出完成后打开文件夹"]="Open Folder after Export",
        ["文件命名"]="File Naming", ["名称来源"]="Name Source", ["使用原文件名"]="Use Original Name", ["使用自定义前缀"]="Use Custom Prefix", ["自定义前缀"]="Custom Prefix", ["附加后缀"]="Suffix", ["起始编号"]="Starting Number", ["编号位数"]="Number Digits",
        ["工作区与恢复"]="Workspace and Recovery", ["启动时自动恢复上次会话"]="Restore Previous Session at Startup", ["自动保存间隔（秒）"]="Auto-save Interval (seconds)",
        ["允许范围 5–300 秒。项目恢复文件保存在当前用户的本地应用数据目录。"]="Allowed range: 5–300 seconds. Recovery data is stored in the current user's local app data folder.",
        ["外观"]="Appearance", ["界面字体"]="Interface Font", ["跟随 Windows 系统"]="Follow Windows System", ["界面语言"]="Interface Language", ["简体中文"]="Simplified Chinese",
        ["字体会应用到主窗口和设置窗口；图标与图片内容不受影响。"]="The font applies to the main and settings windows. Icons and image content are unaffected.",
        ["恢复本页默认值"]="Restore Defaults", ["取消"]="Cancel", ["保存设置"]="Save Settings", ["设置已保存"]="Settings saved",
        ["错误"]="Error", ["导出完成"]="Export Complete", ["设置无效"]="Invalid Settings", ["透明结果检查"]="Transparency Check", ["无法导出"]="Cannot Export",
        ["待检查"]="Pending", ["已确认"]="Confirmed", ["已导出"]="Exported", ["异常"]="Issue", ["保留背景"]="Keep Background"
        , ["项目文件无法打开"]="Could not open project file", ["打开项目失败"]="Open Project Failed", ["无法打开图片"]="Could not open image", ["打开失败"]="Open Failed",
        ["请先添加图片并确认切割框。"]="Add images and confirm crop regions first.", ["导出失败"]="Export failed", ["参数无效"]="Invalid Parameters",
        ["请输入有效的行列、边距和间距。行列范围为 1–20。"]="Enter valid rows, columns, margins, and gaps. Rows and columns must be between 1 and 20.",
        ["边距或间距过大，无法生成有效选框。"]="Margins or gaps are too large to generate valid regions.", ["长图切分参数无效"]="Invalid Long-image Settings",
        ["请先添加图片并启用透明 PNG 导出。"]="Add images and enable transparent PNG export first.", ["检查失败"]="Check failed",
        ["起始编号必须是大于或等于 0 的整数。"]="The starting number must be an integer greater than or equal to 0.",
        ["自动保存间隔必须在 5–300 秒之间。"]="The auto-save interval must be between 5 and 300 seconds.",
        ["已从"]="Exported ", ["张宫格图中导出"]=" source images into ", ["张正方形商品图。"]=" square product images.",
        ["发现"]="Found ", ["个建议复查的切片"]=" slices to review", ["检查完成，没有发现明显异常。"]="Check complete. No obvious issues found."
    };

    public static void SetLanguage(string? code) => LanguageCode = code == "en-US" ? "en-US" : "zh-CN";

    public static string Translate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        IReadOnlyDictionary<string, string> pairs = IsEnglish
            ? Map
            : Map.GroupBy(pair => pair.Value).ToDictionary(group => group.Key, group => group.First().Key);
        if (pairs.TryGetValue(text, out string? exact)) return exact;
        string result = text;
        foreach (var pair in pairs.OrderByDescending(pair => pair.Key.Length)) result = result.Replace(pair.Key, pair.Value);
        return result;
    }

    public static void Apply(Window window)
    {
        window.Title = Translate(window.Title);
        ApplyElement(window, new HashSet<DependencyObject>());
    }

    private static void ApplyElement(DependencyObject element, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(element)) return;
        if (element is TextBlock text) text.Text = Translate(text.Text);
        if (element is ContentControl content && content.Content is string value) content.Content = Translate(value);
        if (element is HeaderedContentControl header && header.Header is string headerText) header.Header = Translate(headerText);
        if (element is FrameworkElement framework)
        {
            if (framework.ToolTip is string tip) framework.ToolTip = Translate(tip);
            if (HintAssist.GetHint(framework) is string hint) HintAssist.SetHint(framework, Translate(hint));
        }
        foreach (object child in LogicalTreeHelper.GetChildren(element))
            if (child is DependencyObject dependency) ApplyElement(dependency, visited);
    }
}
