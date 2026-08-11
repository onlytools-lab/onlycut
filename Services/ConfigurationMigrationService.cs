using CutPic.Models;
using System.IO;

namespace CutPic.Services;

public sealed class UnsupportedConfigurationVersionException(string message) : Exception(message);

public static class ConfigurationMigrationService
{
    public const int CurrentSettingsVersion = 3;
    public const int CurrentProjectVersion = 4;

    public static bool Migrate(AppSettings settings)
    {
        if (settings.SchemaVersion > CurrentSettingsVersion)
            throw new UnsupportedConfigurationVersionException(
                $"Settings version {settings.SchemaVersion} is newer than supported version {CurrentSettingsVersion}.");
        bool changed = settings.SchemaVersion != CurrentSettingsVersion;
        settings.Rows ??= "3"; settings.Columns ??= "3"; settings.MarginX ??= "24"; settings.MarginY ??= "24";
        settings.GapX ??= "18"; settings.GapY ??= "18"; settings.MinimumAreaPercent ??= "0.06";
        settings.ObjectPadding ??= "18"; settings.LongSliceCount ??= "3"; settings.LongFixedHeight ??= "1200";
        settings.LongCustomHeights ??= "800\n1200\n950"; settings.SmartTargetHeight ??= "1200"; settings.SmartSearchRange ??= "240";
        settings.NamingPrefix ??= string.Empty; settings.NamingSuffix ??= string.Empty; settings.NamingStartNumber ??= "1";
        settings.FontFamilyName = string.IsNullOrWhiteSpace(settings.FontFamilyName) ? "System" : settings.FontFamilyName;
        settings.LanguageCode = settings.LanguageCode == "en-US" ? "en-US" : "zh-CN";
        settings.DefaultPresetId = string.IsNullOrWhiteSpace(settings.DefaultPresetId) ? "builtin-card" : settings.DefaultPresetId;
        settings.AutoSaveIntervalSeconds = Math.Clamp(settings.AutoSaveIntervalSeconds <= 0 ? 15 : settings.AutoSaveIntervalSeconds, 5, 300);
        settings.LeftSidebarWidth = Math.Clamp(settings.LeftSidebarWidth <= 0 ? 248 : settings.LeftSidebarWidth, 200, 480);
        settings.RightSidebarWidth = Math.Clamp(settings.RightSidebarWidth <= 0 ? 372 : settings.RightSidebarWidth, 300, 560);
        var shortcuts = ShortcutService.MergeWithDefaults(settings.Shortcuts);
        changed |= settings.Shortcuts is null || shortcuts.Count != settings.Shortcuts.Count || shortcuts.Any(pair =>
            !settings.Shortcuts.TryGetValue(pair.Key, out string? value) || value != pair.Value);
        settings.Shortcuts = shortcuts;
        settings.SchemaVersion = CurrentSettingsVersion;
        return changed;
    }

    public static bool Migrate(ProjectDocument project)
    {
        if (project.Version > CurrentProjectVersion)
            throw new UnsupportedConfigurationVersionException(
                $"Project version {project.Version} is newer than supported version {CurrentProjectVersion}.");
        bool changed = project.Version != CurrentProjectVersion;
        project.Tasks ??= [];
        foreach (ProjectTaskDocument task in project.Tasks)
        {
            task.ImagePath ??= string.Empty; task.Status ??= "待检查"; task.LastError ??= string.Empty;
            task.Transforms ??= []; task.Regions ??= [];
        }
        project.Version = CurrentProjectVersion;
        return changed;
    }
}
