using CutPic.Models;
using CutPic.Services;

namespace CutPic.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void SettingsMigrationAddsDefaultsAndKeepsCustomShortcut()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 0,
            LanguageCode = "invalid",
            Shortcuts = new Dictionary<string, string> { ["OpenImages"] = "Ctrl+P" }
        };

        Assert.True(ConfigurationMigrationService.Migrate(settings));
        Assert.Equal(ConfigurationMigrationService.CurrentSettingsVersion, settings.SchemaVersion);
        Assert.Equal("zh-CN", settings.LanguageCode);
        Assert.Equal("Ctrl+P", settings.Shortcuts["OpenImages"]);
        Assert.InRange(settings.LeftSidebarWidth, 200, 480);
    }

    [Fact]
    public void FutureSettingsVersionIsRejected() =>
        Assert.Throws<UnsupportedConfigurationVersionException>(() =>
            ConfigurationMigrationService.Migrate(new AppSettings { SchemaVersion = 999 }));

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), "访问权限")]
    [InlineData(typeof(PathTooLongException), "路径过长")]
    [InlineData(typeof(InvalidDataException), "损坏")]
    public void FileErrorsAreActionable(Type exceptionType, string expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        Assert.Contains(expected, UserErrorService.Describe(exception, "sample.png"));
    }
}
