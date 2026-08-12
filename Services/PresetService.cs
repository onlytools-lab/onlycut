using System.IO;
using CutPic.Models;
using System.Text.Json;

namespace CutPic.Services;

public static class PresetService
{
    public const int CurrentVersion = 1;
    public static List<ProcessingPreset> Load(string path)
    {
        var presets = BuiltIns();
        try
        {
            if (File.Exists(path))
            {
                using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
                bool legacy = json.RootElement.ValueKind == JsonValueKind.Array;
                PresetDocument document = legacy
                    ? new PresetDocument { Version = 0, Presets = json.RootElement.Deserialize<List<ProcessingPreset>>() ?? [] }
                    : json.RootElement.Deserialize<PresetDocument>() ?? new PresetDocument();
                if (document.Version > CurrentVersion)
                    throw new UnsupportedConfigurationVersionException($"Preset version {document.Version} is newer than supported version {CurrentVersion}.");
                var custom = document.Presets ?? [];
                foreach (var preset in custom.Where(p => !string.IsNullOrWhiteSpace(p.Name)))
                {
                    preset.IsBuiltIn = false;
                    if (string.IsNullOrWhiteSpace(preset.Id)) preset.Id = Guid.NewGuid().ToString("N");
                    presets.Add(preset);
                }
                if (legacy || document.Version != CurrentVersion) Save(path, presets);
            }
        }
        catch (UnsupportedConfigurationVersionException) { throw; }
        catch (Exception ex) { LogService.Error("presets.load_failed", ex); }
        return presets;
    }

    public static void Save(string path, IEnumerable<ProcessingPreset> presets)
    {
        AtomicFileService.WriteJson(path, new PresetDocument
        {
            Version = CurrentVersion,
            Presets = presets.Where(p => !p.IsBuiltIn).ToList()
        });
    }

    public static List<ProcessingPreset> LoadBuiltIns() => BuiltIns();

    private static List<ProcessingPreset> BuiltIns() =>
    [
        new() { Id="builtin-card", Name=LocalizationService.Translate("商品卡片"), IsBuiltIn=true,
            DetectionModeIndex=1, TransparentBackground=false, OutputSizeIndex=1 },
        new() { Id="builtin-borderless", Name=LocalizationService.Translate("无边界图标"), IsBuiltIn=true,
            DetectionModeIndex=2, TransparentBackground=true, TransparencyBackgroundSourceIndex=1,
            DetectionSensitivity=60, GroupingDistance=40, ObjectPadding="18", OutputSizeIndex=1 },
        new() { Id="builtin-emoji", Name=LocalizationService.Translate("表情包"), IsBuiltIn=true,
            DetectionModeIndex=2, TransparentBackground=true, TransparencyBackgroundSourceIndex=1,
            DetectionSensitivity=55, GroupingDistance=32, ObjectPadding="24", OutputSizeIndex=1 },
        new() { Id="builtin-long", Name=LocalizationService.Translate("长图切分"), IsBuiltIn=true,
            WorkModeIndex=1, LongSplitModeIndex=0, LongSliceCount="3", TransparentBackground=false,
            OutputSizeIndex=0 }
    ];
}
