namespace CutPic.Models;

public sealed class ProjectDocument
{
    public int Version { get; set; } = 4;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;
    public List<ProjectTaskDocument> Tasks { get; set; } = [];
}

public sealed class ProjectTaskDocument
{
    public string ImagePath { get; set; } = string.Empty;
    public bool IsLongMode { get; set; }
    public string? SampledBackgroundColor { get; set; }
    public string Status { get; set; } = "待检查";
    public int SliceCount { get; set; }
    public int WarningCount { get; set; }
    public string LastError { get; set; } = string.Empty;
    public List<ImageTransformOperation> Transforms { get; set; } = [];
    public List<CropBoundsDocument> Regions { get; set; } = [];
}

public sealed class CropBoundsDocument
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool RemoveBackground { get; set; } = true;
}
