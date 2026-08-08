namespace CutPic.Models;

public sealed class ProjectDocument
{
    public int Version { get; set; } = 1;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;
    public List<ProjectTaskDocument> Tasks { get; set; } = [];
}

public sealed class ProjectTaskDocument
{
    public string ImagePath { get; set; } = string.Empty;
    public bool IsLongMode { get; set; }
    public string Status { get; set; } = "待检查";
    public List<CropBoundsDocument> Regions { get; set; } = [];
}

public sealed class CropBoundsDocument
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
