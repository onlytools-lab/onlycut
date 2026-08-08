using System.Windows;
using System.Windows.Media;
using CutPic.Services;
using System.IO;
using System.Text;

namespace CutPic;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            MessageBox.Show($"CutPic 发生未处理错误，诊断日志已保存：\n{CrashLogPath}\n\n{args.Exception.Message}",
                "CutPic 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };
        if (e.Args.Length >= 3 && (e.Args[0] == "--smoke" || e.Args[0] == "--smoke-objects"))
        {
            RunSmokeTest(e.Args[1], e.Args[2], e.Args[0] == "--smoke-objects");
            Shutdown();
            return;
        }
        if (e.Args.Length >= 5 && e.Args[0] == "--smoke-long")
        {
            RunLongSmokeTest(e.Args[1], e.Args[2], int.Parse(e.Args[3]), e.Args[4]);
            Shutdown();
            return;
        }
        try
        {
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            MessageBox.Show($"CutPic 无法启动，诊断日志已保存：\n{CrashLogPath}\n\n{ex.Message}",
                "CutPic 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CutPic", "crash.log");

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            var text = new StringBuilder()
                .AppendLine($"Time: {DateTimeOffset.Now:O}")
                .AppendLine($"OS: {Environment.OSVersion}")
                .AppendLine($"Runtime: {Environment.Version}")
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(CrashLogPath, text);
        }
        catch { }
    }

    private static void RunSmokeTest(string inputPath, string outputDirectory, bool objectMode)
    {
        Directory.CreateDirectory(outputDirectory);
        var bitmap = ImageProcessingService.LoadImage(inputPath);
        var regions = objectMode
            ? ImageProcessingService.DetectObjects(bitmap, 60, 40, .06, 18)
            : ImageProcessingService.DetectCards(bitmap);
        string name = Path.GetFileNameWithoutExtension(inputPath);
        for (int i = 0; i < regions.Count; i++)
        {
            var square = ImageProcessingService.CreateSquare(bitmap, regions[i].Bounds, 512, true, Colors.White);
            ImageProcessingService.Save(square, Path.Combine(outputDirectory, $"{name}_{i + 1:00}.png"), "PNG", 92);
        }
        File.WriteAllText(Path.Combine(outputDirectory, $"{name}_result.txt"),
            $"Detected={regions.Count}{Environment.NewLine}" +
            string.Join(Environment.NewLine, regions.Select(r => $"{r.Number}: {r.Bounds}")));
    }

    private static void RunLongSmokeTest(string inputPath, string outputDirectory, int mode, string value)
    {
        Directory.CreateDirectory(outputDirectory);
        var bitmap = ImageProcessingService.LoadImage(inputPath);
        int count = mode == 0 ? int.Parse(value) : 3;
        int fixedHeight = mode == 1 ? int.Parse(value) : 1200;
        int[] custom = mode == 2
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
            : [];
        var regions = CutPic.MainWindow.BuildLongRegions(bitmap.PixelWidth, bitmap.PixelHeight, mode, count, fixedHeight, custom);
        string name = Path.GetFileNameWithoutExtension(inputPath);
        for (int i = 0; i < regions.Count; i++)
        {
            var crop = ImageProcessingService.CreateCrop(bitmap, regions[i].Bounds);
            ImageProcessingService.Save(crop, Path.Combine(outputDirectory, $"{name}_part_{i + 1:00}.png"), "PNG", 92);
        }
        File.WriteAllText(Path.Combine(outputDirectory, $"{name}_long_result.txt"),
            $"Source={bitmap.PixelWidth}x{bitmap.PixelHeight}{Environment.NewLine}" +
            $"Slices={regions.Count}{Environment.NewLine}" +
            $"TotalHeight={regions.Sum(r => r.Bounds.Height)}{Environment.NewLine}" +
            string.Join(Environment.NewLine, regions.Select(r => $"{r.Number}: {r.Bounds}")));
    }
}
