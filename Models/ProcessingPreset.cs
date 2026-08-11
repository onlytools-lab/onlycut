namespace CutPic.Models;

public sealed class ProcessingPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public int WorkModeIndex { get; set; }
    public int DetectionModeIndex { get; set; }
    public double DetectionSensitivity { get; set; } = 60;
    public double GroupingDistance { get; set; } = 40;
    public string MinimumAreaPercent { get; set; } = "0.06";
    public string ObjectPadding { get; set; } = "18";
    public bool TransparentBackground { get; set; }
    public int TransparencyBackgroundSourceIndex { get; set; }
    public int TransparencyStrengthIndex { get; set; } = 1;
    public double TransparencyTolerance { get; set; } = 10;
    public double TransparencyFeather { get; set; } = 16;
    public int OutputSizeIndex { get; set; } = 1;
    public int FormatIndex { get; set; }
    public int LongSplitModeIndex { get; set; }
    public string LongSliceCount { get; set; } = "3";
    public string LongFixedHeight { get; set; } = "1200";
    public string LongCustomHeights { get; set; } = "800\n1200\n950";
    public string SmartTargetHeight { get; set; } = "1200";
    public string SmartSearchRange { get; set; } = "240";
    public int NamingModeIndex { get; set; }
    public string NamingPrefix { get; set; } = string.Empty;
    public string NamingSuffix { get; set; } = string.Empty;
    public string NamingStartNumber { get; set; } = "1";
    public int NamingDigitsIndex { get; set; }
    public override string ToString() => Name;
}
