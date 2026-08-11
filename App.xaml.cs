using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CutPic.Services;
using System.IO;
using System.Text;
using CutPic.Models;

namespace CutPic;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            MessageBox.Show(LocalizationService.Translate($"CutPic 发生未处理错误，诊断日志已保存：\n{CrashLogPath}\n\n{args.Exception.Message}"),
                LocalizationService.Translate("CutPic 启动错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };
        if (e.Args.Length >= 1 && e.Args[0] == "--smoke-settings")
        {
            LocalizationService.SetLanguage(e.Args.Length >= 2 ? e.Args[1] : "zh-CN");
            var window = new SettingsWindow(new CutPic.Models.AppSettings());
            window.Show(); window.Close(); Shutdown(); return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--smoke-presets")
        {
            string path = e.Args[1];
            var presets = PresetService.Load(path);
            presets.Add(new CutPic.Models.ProcessingPreset { Name = "Smoke Custom", DetectionModeIndex = 2 });
            PresetService.Save(path, presets);
            var loaded = PresetService.Load(path);
            if (loaded.Count != 5 || loaded.Count(p => p.IsBuiltIn) != 4 || loaded.All(p => p.Name != "Smoke Custom"))
                throw new InvalidDataException("Preset smoke test failed.");
            Shutdown(); return;
        }
        if (e.Args.Length >= 1 && e.Args[0] == "--smoke-main")
        {
            var window = new MainWindow(); window.Show(); window.Close(); Shutdown(); return;
        }
        if (e.Args.Length >= 1 && e.Args[0] == "--smoke-long-edit")
        {
            RunLongEditSmokeTest(); Shutdown(); return;
        }
        if (e.Args.Length >= 1 && e.Args[0] == "--smoke-exif-atomic")
        {
            RunExifSmokeTest(); RunAtomicFileSmokeTest(); Shutdown(); return;
        }
        if (e.Args.Length >= 1 && e.Args[0] == "--smoke-export-preview")
        {
            LocalizationService.SetLanguage(e.Args.Length >= 2 ? e.Args[1] : "zh-CN");
            var pixel = new byte[] { 255, 255, 255, 255 };
            var image = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null, pixel, 4); image.Freeze();
            var item = new CutPic.Models.ExportPreviewItem { SourcePath="smoke.png", SourceName="smoke.png",
                FileName="smoke_01.png", RegionIndex=0, RegionNumber=1, Thumbnail=image, Detail="Transparent PNG" };
            var window = new ExportPreviewWindow([item]); window.Show(); window.Close(); Shutdown(); return;
        }
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
        if (e.Args.Length >= 3 && (e.Args[0] == "--smoke-transparent" || e.Args[0] == "--smoke-transparent-card"))
        {
            RunTransparentSmokeTest(e.Args[1], e.Args[2],
                e.Args[0] == "--smoke-transparent-card"
                    ? TransparencyBackgroundSource.Card
                    : TransparencyBackgroundSource.Borderless);
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
            MessageBox.Show(LocalizationService.Translate($"CutPic 无法启动，诊断日志已保存：\n{CrashLogPath}\n\n{ex.Message}"),
                LocalizationService.Translate("CutPic 启动错误"), MessageBoxButton.OK, MessageBoxImage.Error);
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
        var regions = mode == 3
            ? ImageProcessingService.SuggestLongImageSlices(bitmap,
                int.Parse(value.Split(',')[0]), int.Parse(value.Split(',')[1]))
            : CutPic.MainWindow.BuildLongRegions(bitmap.PixelWidth, bitmap.PixelHeight, mode, count, fixedHeight, custom);
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

    private static void RunLongEditSmokeTest()
    {
        const int width = 600, height = 900;
        var regions = CutPic.MainWindow.BuildLongRegions(width, height, 0, 3, 0, []);
        if (!CutPic.MainWindow.TryInsertLongDivider(regions, width, 150, out int inserted) || inserted != 0 || regions.Count != 4)
            throw new InvalidDataException("Long divider insertion failed.");
        CutPic.MainWindow.MoveLongDivider(regions, width, 1, 350);
        CutPic.MainWindow.RemoveLongDivider(regions, width, 0);
        if (regions.Count != 3 || Math.Abs(regions[0].Bounds.Height - 350) > .01)
            throw new InvalidDataException("Long divider move/remove failed.");
        double y = 0;
        foreach (CropRegion region in regions)
        {
            if (region.Bounds.X != 0 || region.Bounds.Width != width || region.Bounds.Y != y || region.Bounds.Height < 20)
                throw new InvalidDataException("Long divider continuity failed.");
            y = region.Bounds.Bottom;
        }
        if (Math.Abs(y - height) > .01) throw new InvalidDataException("Long divider total height changed.");
    }

    private static void RunExifSmokeTest()
    {
        byte[] pixels = new byte[2 * 3 * 4];
        for (int i = 0; i < 6; i++) { pixels[i * 4] = (byte)(i + 1); pixels[i * 4 + 3] = 255; }
        var source = BitmapSource.Create(2, 3, 96, 96, PixelFormats.Bgra32, null, pixels, 8); source.Freeze();
        AssertOrientation(2, 2, 3, [2, 1, 4, 3, 6, 5]);
        AssertOrientation(3, 2, 3, [6, 5, 4, 3, 2, 1]);
        AssertOrientation(4, 2, 3, [5, 6, 3, 4, 1, 2]);
        AssertOrientation(5, 3, 2, [1, 3, 5, 2, 4, 6]);
        AssertOrientation(6, 3, 2, [5, 3, 1, 6, 4, 2]);
        AssertOrientation(7, 3, 2, [6, 4, 2, 5, 3, 1]);
        AssertOrientation(8, 3, 2, [2, 4, 6, 1, 3, 5]);

        string directory = Path.Combine(Path.GetTempPath(), $"CutPicExifSmoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "orientation-6.jpg");
            var larger = new TransformedBitmap(source, new ScaleTransform(10, 10)); larger.Freeze();
            var metadata = new BitmapMetadata("jpg");
            metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(larger, null, metadata, null));
            using (var stream = File.Create(path)) encoder.Save(stream);
            BitmapSource loaded = ImageProcessingService.LoadImage(path);
            if (loaded.PixelWidth != 30 || loaded.PixelHeight != 20)
                throw new InvalidDataException("JPEG EXIF orientation metadata was not applied.");
        }
        finally { Directory.Delete(directory, true); }

        void AssertOrientation(ushort orientation, int width, int height, byte[] expected)
        {
            BitmapSource result = ImageProcessingService.ApplyExifOrientation(source, orientation);
            if (result.PixelWidth != width || result.PixelHeight != height) throw new InvalidDataException($"EXIF {orientation} dimensions failed.");
            var actual = new byte[width * height * 4]; result.CopyPixels(actual, width * 4, 0);
            for (int i = 0; i < expected.Length; i++)
                if (actual[i * 4] != expected[i]) throw new InvalidDataException($"EXIF {orientation} pixel mapping failed at {i}.");
        }
    }

    private static void RunAtomicFileSmokeTest()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"CutPicAtomicSmoke-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "settings.json");
        try
        {
            AtomicFileService.WriteJson(path, new Dictionary<string, int> { ["Value"] = 1 });
            AtomicFileService.WriteJson(path, new Dictionary<string, int> { ["Value"] = 2 });
            var current = AtomicFileService.ReadJson<Dictionary<string, int>>(path);
            var backup = AtomicFileService.ReadJson<Dictionary<string, int>>(AtomicFileService.BackupPath(path));
            if (current?["Value"] != 2 || backup?["Value"] != 1) throw new InvalidDataException("Atomic write or backup failed.");
            File.WriteAllText(path, "{");
            var recovered = AtomicFileService.ReadJson<Dictionary<string, int>>(path);
            if (recovered?["Value"] != 1) throw new InvalidDataException("Atomic backup recovery failed.");
            if (Directory.EnumerateFiles(directory, "*.tmp").Any()) throw new InvalidDataException("Atomic write left temporary files.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static void RunTransparentSmokeTest(string inputPath, string outputDirectory,
        TransparencyBackgroundSource backgroundSource)
    {
        Directory.CreateDirectory(outputDirectory);
        var bitmap = ImageProcessingService.LoadImage(inputPath);
        var regions = backgroundSource == TransparencyBackgroundSource.Card
            ? ImageProcessingService.DetectCards(bitmap)
            : ImageProcessingService.DetectObjects(bitmap, 60, 40, .06, 18);
        string name = Path.GetFileNameWithoutExtension(inputPath);
        var report = new List<string> { $"Detected={regions.Count}" };
        for (int i = 0; i < regions.Count; i++)
        {
            var output = ImageProcessingService.CreateTransparentSquare(bitmap, regions[i].Bounds, 512, 10, 16,
                backgroundSource);
            string path = Path.Combine(outputDirectory, $"{name}_transparent_{i + 1:00}.png");
            ImageProcessingService.Save(output, path, "PNG", 92);
            int stride = output.PixelWidth * 4;
            var pixels = new byte[stride * output.PixelHeight];
            output.CopyPixels(pixels, stride, 0);
            int clear = 0, partial = 0, opaque = 0;
            for (int p = 3; p < pixels.Length; p += 4)
            {
                if (pixels[p] == 0) clear++;
                else if (pixels[p] == 255) opaque++;
                else partial++;
            }
            report.Add($"{i + 1}: Clear={clear}, Partial={partial}, Opaque={opaque}");
        }
        File.WriteAllLines(Path.Combine(outputDirectory, $"{name}_transparent_result.txt"), report);
    }
}
