using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CutPic.Services;
using System.IO;
using System.Text;
using CutPic.Models;
using System.Diagnostics;

namespace CutPic;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() is "--self-test" or "--acceptance") ShutdownMode = ShutdownMode.OnExplicitShutdown;
        LogService.Initialize();
        if (e.Args.Length >= 2 && e.Args[0] == "--acceptance")
        {
            Shutdown(RunAcceptance(e.Args[1])); return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            LogService.Error("app.unhandled_exception", args.Exception);
            MessageBox.Show(LocalizationService.Translate($"CutPic 发生未处理错误，诊断日志已保存：\n{CrashLogPath}\n\n{args.Exception.Message}"),
                LocalizationService.Translate("CutPic 启动错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };
        if (e.Args.Length >= 1 && e.Args[0] == "--self-test")
        {
            string reportPath = e.Args.Length >= 2 ? e.Args[1] : Path.Combine(Path.GetTempPath(), "CutPic-self-test.txt");
            Shutdown(RunSelfTests(reportPath)); return;
        }
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
            RunExifSmokeTest(); RunManualTransformSmokeTest(); RunAtomicFileSmokeTest(); Shutdown(); return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--smoke-thumbnail")
        {
            BitmapSource thumbnail = ImageProcessingService.LoadThumbnail(e.Args[1], 64);
            if (thumbnail.PixelWidth > 64 || thumbnail.PixelHeight > 64 || !thumbnail.IsFrozen)
                throw new InvalidDataException("Thumbnail size or thread-safety validation failed.");
            Shutdown(); return;
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
            LogService.Error("app.start_failed", ex);
            MessageBox.Show(LocalizationService.Translate($"CutPic 无法启动，诊断日志已保存：\n{CrashLogPath}\n\n{ex.Message}"),
                LocalizationService.Translate("CutPic 启动错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Info("app.exit", $"Exit code: {e.ApplicationExitCode}");
        base.OnExit(e);
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

    private static int RunSelfTests(string reportPath)
    {
        var report = new List<string>
        {
            $"CutPic self-test", $"Version: {typeof(App).Assembly.GetName().Version}",
            $"Runtime: {Environment.Version}", $"Started: {DateTimeOffset.Now:O}", string.Empty
        };
        int failed = 0;
        void Test(string name, Action action)
        {
            var timer = Stopwatch.StartNew();
            try { action(); report.Add($"PASS  {name}  ({timer.ElapsedMilliseconds} ms)"); }
            catch (Exception ex) { failed++; report.Add($"FAIL  {name}  ({timer.ElapsedMilliseconds} ms)\n      {ex}"); }
        }

        string isolatedData = Path.Combine(Path.GetTempPath(), $"CutPicSelfTest-{Guid.NewGuid():N}");
        string? previousData = Environment.GetEnvironmentVariable("CUTPIC_DATA_DIR");
        Environment.SetEnvironmentVariable("CUTPIC_DATA_DIR", isolatedData);
        try
        {
            Test("EXIF orientations and metadata", RunExifSmokeTest);
            Test("Manual transform mapping", RunManualTransformSmokeTest);
            Test("Long-image divider continuity", RunLongEditSmokeTest);
            Test("Atomic write and backup recovery", RunAtomicFileSmokeTest);
            Test("Structured logs and diagnostic package", () =>
            {
                LogService.Initialize();
                string package = Path.Combine(isolatedData, "diagnostics.zip");
                LogService.CreateDiagnosticPackage(package, new AppSettings());
                using var archive = System.IO.Compression.ZipFile.OpenRead(package);
                if (archive.GetEntry("system-info.txt") is null || archive.GetEntry("settings-summary.json") is null ||
                    archive.Entries.Any(entry => entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                 entry.Name.EndsWith(".cutpic", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Diagnostic package validation failed.");
            });
            Test("Actionable error messages", () =>
            {
                if (!UserErrorService.Describe(new UnauthorizedAccessException(), "x.png").Contains("访问权限") ||
                    !UserErrorService.Describe(new PathTooLongException()).Contains("路径过长") ||
                    !UserErrorService.Describe(new InvalidDataException()).Contains("损坏"))
                    throw new InvalidDataException("Friendly error classification failed.");
            });
            Test("Settings and project migration", RunMigrationSmokeTest);
            Test("Image output and thumbnail pipeline", RunImagePipelineSmokeTest);
            Test("Shortcut defaults and export naming", RunConfigurationSmokeTest);
            Test("Preset persistence", () =>
            {
                string path = Path.Combine(isolatedData, "presets.json");
                AtomicFileService.WriteJson(path, new List<ProcessingPreset> { new() { Name = "Legacy" } });
                if (PresetService.Load(path).All(preset => preset.Name != "Legacy")) throw new InvalidDataException("Legacy preset migration failed.");
                var presets = PresetService.Load(path);
                presets.Add(new ProcessingPreset { Name = "Self Test", DetectionModeIndex = 2 });
                PresetService.Save(path, presets);
                if (PresetService.Load(path).All(preset => preset.Name != "Self Test")) throw new InvalidDataException("Preset persistence failed.");
                AtomicFileService.WriteJson(path, new PresetDocument { Version = 999 });
                try { PresetService.Load(path); throw new InvalidDataException("Future preset version was accepted."); }
                catch (UnsupportedConfigurationVersionException) { }
            });
            Test("Chinese and English settings windows", () =>
            {
                LocalizationService.SetLanguage("zh-CN"); var zh = new SettingsWindow(new AppSettings()); zh.Show(); zh.Close();
                LocalizationService.SetLanguage("en-US"); var en = new SettingsWindow(new AppSettings()); en.Show(); en.Close();
            });
            Test("Main window startup", () => { var window = new MainWindow(); window.Show(); window.Close(); });
            Test("Responsive window layouts", () =>
            {
                var main = new MainWindow { Width = 960, Height = 560 };
                main.ApplyResponsiveToolbarForWidth(960);
                if (main.MinWidth > 960 || main.MinHeight > 560 || main.MoreActionsButton.Visibility != Visibility.Visible ||
                    main.ExportAllButtonText.Visibility != Visibility.Collapsed || main.AppSubtitleText.Visibility != Visibility.Collapsed ||
                    main.DetectButton.IsEnabled || main.ExportAllButton.IsEnabled || main.RemoveTaskButton.IsEnabled)
                    throw new InvalidDataException($"Compact main-window layout failed: more={main.MoreActionsButton.Visibility}, exportText={main.ExportAllButtonText.Visibility}, subtitle={main.AppSubtitleText.Visibility}.");
                var settings = new SettingsWindow(new AppSettings()) { Width = 620, Height = 520 };
                if (settings.MinWidth > 620 || settings.MinHeight > 520)
                    throw new InvalidDataException("Compact settings layout failed.");
                var preview = new ExportPreviewWindow([]) { Width = 680, Height = 500 };
                if (preview.MinWidth > 680 || preview.MinHeight > 500)
                    throw new InvalidDataException("Compact export-preview layout failed.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CUTPIC_DATA_DIR", previousData);
            try { if (Directory.Exists(isolatedData)) Directory.Delete(isolatedData, true); } catch { }
        }
        report.Add(string.Empty); report.Add(failed == 0 ? "RESULT: PASSED" : $"RESULT: FAILED ({failed})");
        AtomicFileService.WriteAllText(reportPath, string.Join(Environment.NewLine, report));
        return failed == 0 ? 0 : -1;
    }

    private static void RunMigrationSmokeTest()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 0, LanguageCode = "unsupported", AutoSaveIntervalSeconds = 0,
            Shortcuts = new Dictionary<string, string> { ["OpenImages"] = "Ctrl+P" }
        };
        if (!ConfigurationMigrationService.Migrate(settings) || settings.SchemaVersion != ConfigurationMigrationService.CurrentSettingsVersion ||
            settings.LanguageCode != "zh-CN" || settings.AutoSaveIntervalSeconds != 15 ||
            settings.Shortcuts.Count != ShortcutService.Definitions.Count || settings.Shortcuts["OpenImages"] != "Ctrl+P")
            throw new InvalidDataException("Settings migration failed.");
        var project = new ProjectDocument { Version = 3, Tasks = [new ProjectTaskDocument { Transforms = null!, Regions = null! }] };
        if (!ConfigurationMigrationService.Migrate(project) || project.Version != 4 || project.Tasks[0].Transforms is null || project.Tasks[0].Regions is null)
            throw new InvalidDataException("Project migration failed.");
        try
        {
            ConfigurationMigrationService.Migrate(new AppSettings { SchemaVersion = 999 });
            throw new InvalidDataException("Future settings version was accepted.");
        }
        catch (UnsupportedConfigurationVersionException) { }
    }

    private static void RunImagePipelineSmokeTest()
    {
        const int width = 80, height = 50, stride = width * 4;
        var pixels = new byte[stride * height];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 210; pixels[i + 1] = 170; pixels[i + 2] = 80; pixels[i + 3] = 255; }
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride); source.Freeze();
        BitmapSource square = ImageProcessingService.CreateSquare(source, new Rect(5, 5, 60, 35), 64, false, Colors.White);
        if (square.PixelWidth != 64 || square.PixelHeight != 64 || !square.IsFrozen) throw new InvalidDataException("Square output failed.");
        string directory = Path.Combine(Path.GetTempPath(), $"CutPicPipeline-{Guid.NewGuid():N}"); Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "source.png"); ImageProcessingService.Save(source, path, "PNG", 92);
            BitmapSource thumbnail = ImageProcessingService.LoadThumbnail(path, 32);
            if (thumbnail.PixelWidth > 32 || thumbnail.PixelHeight > 32 || !thumbnail.IsFrozen) throw new InvalidDataException("Thumbnail pipeline failed.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void RunConfigurationSmokeTest()
    {
        var shortcuts = ShortcutService.MergeWithDefaults(new Dictionary<string, string>());
        if (shortcuts.Count != ShortcutService.Definitions.Count || shortcuts.Values.Select(ShortcutService.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Count() != shortcuts.Count)
            throw new InvalidDataException("Shortcut defaults are incomplete or conflicting.");
        string name = CutPic.MainWindow.BuildExportFileName("商品", true, 0, ".png", 0, string.Empty, "x", 5, 3);
        if (name != "商品_part_005_x.png") throw new InvalidDataException($"Export naming failed: {name}");
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

    private static void RunManualTransformSmokeTest()
    {
        Rect source = new(10, 20, 30, 40);
        if (CutPic.MainWindow.TransformBounds(source, 100, 200, ImageTransformOperation.RotateRight) != new Rect(140, 10, 40, 30) ||
            CutPic.MainWindow.TransformBounds(source, 100, 200, ImageTransformOperation.RotateLeft) != new Rect(20, 60, 40, 30) ||
            CutPic.MainWindow.TransformBounds(source, 100, 200, ImageTransformOperation.FlipHorizontal) != new Rect(60, 20, 30, 40) ||
            CutPic.MainWindow.TransformBounds(source, 100, 200, ImageTransformOperation.FlipVertical) != new Rect(10, 140, 30, 40))
            throw new InvalidDataException("Manual transform crop mapping failed.");
        var transforms = new List<ImageTransformOperation>();
        CutPic.MainWindow.AppendTransform(transforms, ImageTransformOperation.RotateRight);
        CutPic.MainWindow.AppendTransform(transforms, ImageTransformOperation.RotateLeft);
        CutPic.MainWindow.AppendTransform(transforms, ImageTransformOperation.FlipHorizontal);
        CutPic.MainWindow.AppendTransform(transforms, ImageTransformOperation.FlipHorizontal);
        if (transforms.Count != 0) throw new InvalidDataException("Manual transform normalization failed.");
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
