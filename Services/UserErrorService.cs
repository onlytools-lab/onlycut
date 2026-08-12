using System.IO;
using System.Runtime.InteropServices;

namespace CutPic.Services;

public static class UserErrorService
{
    public static string Describe(Exception exception, string? path = null)
    {
        Exception root = Unwrap(exception);
        string target = string.IsNullOrWhiteSpace(path) ? string.Empty : $"\n文件：{path}";
        string advice = root switch
        {
            FileNotFoundException or DirectoryNotFoundException => "文件或所在文件夹已经不存在，请重新选择文件。",
            UnauthorizedAccessException => "没有访问权限。请选择有权限的文件或文件夹，或更换导出位置。",
            PathTooLongException => "文件路径过长。请缩短文件名，或将文件移动到层级较浅的文件夹。",
            IOException when IsSharingViolation(root) => "文件正被其他程序占用。请关闭占用该文件的程序后重试。",
            IOException when IsDiskFull(root) => "磁盘空间不足。请清理磁盘或更换输出位置。",
            IOException => "读取或写入文件失败。请检查文件是否被占用、磁盘是否可用。",
            NotSupportedException => "图片格式、像素格式或色彩配置不受支持。建议先转换为标准 PNG 或 JPG。",
            FileFormatException or InvalidDataException => "文件已损坏，或者它并不是有效的图片文件。建议用图片查看器确认后重新导出。",
            OutOfMemoryException => "图片尺寸或解码所需内存过大。请关闭其他程序，或先降低图片分辨率。",
            ArgumentException => "图片数据无效，可能包含不兼容的编码或色彩配置。建议转换为标准 RGB PNG/JPG。",
            _ => "处理文件时发生错误。可在“设置 → 关于”中导出诊断包以便排查。"
        };
        return $"{advice}{target}\n\n技术信息：{root.Message}";
    }

    public static void Log(string eventName, Exception exception, string? path = null) =>
        LogService.Error(eventName, exception, path is null ? exception.Message : $"{path}: {exception.Message}");

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException { InnerExceptions.Count: 1 } aggregate) exception = aggregate.InnerExceptions[0];
        return exception.InnerException is not null && exception is System.Reflection.TargetInvocationException
            ? Unwrap(exception.InnerException) : exception;
    }

    private static bool IsSharingViolation(Exception exception) =>
        exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021);

    private static bool IsDiskFull(Exception exception) =>
        exception.HResult is unchecked((int)0x80070027) or unchecked((int)0x80070070);
}
