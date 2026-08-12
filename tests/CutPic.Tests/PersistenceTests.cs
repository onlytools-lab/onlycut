using CutPic.Models;
using CutPic.Services;

namespace CutPic.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void AtomicWriteRetainsBackup()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"CutPicTests-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "value.json");
        try
        {
            AtomicFileService.WriteJson(path, new { Value = 1 });
            AtomicFileService.WriteJson(path, new { Value = 2 });
            Assert.True(File.Exists(path + ".bak"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void LegacyPresetArrayMigratesToVersionedDocument()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"CutPicTests-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "presets.json");
        try
        {
            AtomicFileService.WriteJson(path, new List<ProcessingPreset> { new() { Name = "Legacy" } });
            Assert.Contains(PresetService.Load(path), preset => preset.Name == "Legacy");
            PresetDocument document = AtomicFileService.ReadJson<PresetDocument>(path)!;
            Assert.Equal(PresetService.CurrentVersion, document.Version);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
