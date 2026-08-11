using System.Text;
using System.Text.Json;
using System.IO;

namespace CutPic.Services;

public static class AtomicFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void WriteJson<T>(string path, T value) =>
        WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

    public static T? ReadJson<T>(string path)
    {
        Exception? primaryError = null;
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            primaryError = ex;
        }

        string backup = BackupPath(path);
        try
        {
            if (File.Exists(backup)) return JsonSerializer.Deserialize<T>(File.ReadAllText(backup));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            if (primaryError is not null) throw new AggregateException(primaryError, ex);
            throw;
        }
        if (primaryError is not null) throw primaryError;
        return default;
    }

    public static void WriteAllText(string path, string content)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("File path has no directory.", nameof(path));
        Directory.CreateDirectory(directory);
        string temp = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(fullPath))
            {
                string backup = BackupPath(fullPath);
                try { File.Replace(temp, fullPath, backup, true); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(fullPath, backup, true);
                    File.Move(temp, fullPath, true);
                }
            }
            else File.Move(temp, fullPath);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static string BackupPath(string path) => path + ".bak";
}
