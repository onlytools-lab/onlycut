using CutPic.Models;
using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace CutPic.Services;

public static class LogService
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string DataDirectory => Environment.GetEnvironmentVariable("CUTPIC_DATA_DIR")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CutPic");
    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    private static string CurrentLogPath => Path.Combine(LogsDirectory, $"cutpic-{DateTime.Now:yyyy-MM-dd}.jsonl");

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            foreach (string path in Directory.EnumerateFiles(LogsDirectory, "cutpic-*.jsonl"))
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-14)) File.Delete(path);
            Info("app.start", "CutPic started");
        }
        catch { }
    }

    public static void Info(string eventName, string message) => Write("info", eventName, message, null);
    public static void Error(string eventName, Exception exception, string? message = null) =>
        Write("error", eventName, message ?? exception.Message, exception);

    private static void Write(string level, string eventName, string message, Exception? exception)
    {
        try
        {
            var entry = new
            {
                timestamp = DateTimeOffset.Now,
                level,
                eventName,
                message,
                exception = exception?.ToString(),
                processId = Environment.ProcessId,
                threadId = Environment.CurrentManagedThreadId
            };
            string line = JsonSerializer.Serialize(entry);
            lock (Gate)
            {
                Directory.CreateDirectory(LogsDirectory);
                File.AppendAllText(CurrentLogPath, line + Environment.NewLine);
            }
        }
        catch { }
    }

    public static void CreateDiagnosticPackage(string destinationPath, AppSettings settings)
    {
        string staging = Path.Combine(Path.GetTempPath(), $"CutPicDiagnostics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            File.WriteAllText(Path.Combine(staging, "system-info.txt"),
                $"Generated: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                $"CutPic: {assembly.Version}{Environment.NewLine}" +
                $"OS: {Environment.OSVersion}{Environment.NewLine}" +
                $"Runtime: {Environment.Version}{Environment.NewLine}" +
                $"64-bit process: {Environment.Is64BitProcess}{Environment.NewLine}" +
                $"Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}{Environment.NewLine}");
            var summary = new
            {
                settings.SchemaVersion, settings.LanguageCode, settings.FontFamilyName,
                settings.OutputSizeIndex, settings.FormatIndex, settings.WorkModeIndex,
                settings.DetectionModeIndex, settings.TransparentBackground,
                settings.LeftSidebarWidth, settings.RightSidebarWidth,
                settings.IsLeftSidebarCollapsed, settings.IsRightSidebarCollapsed
            };
            File.WriteAllText(Path.Combine(staging, "settings-summary.json"), JsonSerializer.Serialize(summary, JsonOptions));
            if (Directory.Exists(LogsDirectory))
                foreach (string log in Directory.EnumerateFiles(LogsDirectory, "*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).Take(7))
                    File.Copy(log, Path.Combine(staging, Path.GetFileName(log)), true);
            string crash = Path.Combine(DataDirectory, "crash.log");
            if (File.Exists(crash)) File.Copy(crash, Path.Combine(staging, "crash.log"), true);
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            ZipFile.CreateFromDirectory(staging, destinationPath, CompressionLevel.Optimal, false);
            Info("diagnostics.export", "Diagnostic package created");
        }
        finally
        {
            try { Directory.Delete(staging, true); } catch { }
        }
    }
}
