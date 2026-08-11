namespace CutPic.Models;

public sealed class AppSettings
{
    public int OutputSizeIndex { get; set; } = 1;
    public int FormatIndex { get; set; }
    public double JpegQuality { get; set; } = 92;
    public bool AutoBackground { get; set; } = true;
    public string Rows { get; set; } = "3";
    public string Columns { get; set; } = "3";
    public string MarginX { get; set; } = "24";
    public string MarginY { get; set; } = "24";
    public string GapX { get; set; } = "18";
    public string GapY { get; set; } = "18";
    public double Zoom { get; set; } = 1;
    public int DetectionModeIndex { get; set; }
    public double DetectionSensitivity { get; set; } = 60;
    public double GroupingDistance { get; set; } = 40;
    public string MinimumAreaPercent { get; set; } = "0.06";
    public string ObjectPadding { get; set; } = "18";
    public int WorkModeIndex { get; set; }
    public int LongSplitModeIndex { get; set; }
    public string LongSliceCount { get; set; } = "3";
    public string LongFixedHeight { get; set; } = "1200";
    public string LongCustomHeights { get; set; } = "800\n1200\n950";
    public int ConflictPolicyIndex { get; set; }
    public string SmartTargetHeight { get; set; } = "1200";
    public string SmartSearchRange { get; set; } = "240";
    public bool TransparentBackground { get; set; }
    public int TransparencyBackgroundSourceIndex { get; set; }
    public int TransparencyStrengthIndex { get; set; } = 1;
    public double TransparencyTolerance { get; set; } = 10;
    public double TransparencyFeather { get; set; } = 16;
    public int NamingModeIndex { get; set; }
    public string NamingPrefix { get; set; } = string.Empty;
    public string NamingSuffix { get; set; } = string.Empty;
    public string NamingStartNumber { get; set; } = "1";
    public int NamingDigitsIndex { get; set; }
    public bool CreateSourceSubfolder { get; set; }
    public bool OpenFolderAfterExport { get; set; } = true;
    public bool AutoRestoreSession { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 15;
    public string FontFamilyName { get; set; } = "System";
    public string LanguageCode { get; set; } = "zh-CN";
    public string DefaultPresetId { get; set; } = "builtin-card";
    public bool OnlyConfirmedTasks { get; set; }
}
