using CutPic.Models;
using CutPic.Services;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CutPic;

public partial class App
{
    private static int RunAcceptance(string reportPath)
    {
        string directory = Path.Combine(Path.GetTempPath(), "OnlyCutAcceptance-" + Guid.NewGuid().ToString("N"));
        string? previous = Environment.GetEnvironmentVariable("CUTPIC_DATA_DIR");
        Environment.SetEnvironmentVariable("CUTPIC_DATA_DIR", directory);
        Directory.CreateDirectory(directory);
        var report = new List<string> { "OnlyCut synthetic pipeline acceptance (not manual UI acceptance)" };
        int failures = 0;
        void Check(string name, Action action)
        {
            try { action(); report.Add("PASS " + name); }
            catch (Exception ex) { failures++; report.Add("FAIL " + name + ": " + ex); }
        }
        try
        {
            const int width = 600, height = 800;
            BitmapSource Fixture(bool cards)
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(cards ? Brushes.Black : Brushes.White, null, new Rect(0, 0, width, height));
                    for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        if (cards) dc.DrawRoundedRectangle(Brushes.White, null, new Rect(20 + col * 300, 20 + row * 400, 260, 360), 10, 10);
                        dc.DrawEllipse(Brushes.CornflowerBlue, null, new Point(150 + col * 300, 200 + row * 400), 60, 75);
                    }
                }
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual); bitmap.Freeze(); return bitmap;
            }
            string source = Path.Combine(directory, "source.png");
            ImageProcessingService.Save(Fixture(true), source, "PNG", 92);
            Check("image import, 4-card detection and batch square exports", () =>
            {
                var bitmap = ImageProcessingService.LoadImage(source);
                var regions = ImageProcessingService.DetectCards(bitmap);
                if (regions.Count != 4) throw new InvalidDataException($"Expected 4 cards, got {regions.Count}");
                for (int i=0; i<regions.Count; i++)
                {
                    string output = Path.Combine(directory, $"square-{i}.png");
                    ImageProcessingService.Save(ImageProcessingService.CreateSquare(bitmap, regions[i].Bounds, 512, true, Colors.White), output, "PNG", 92);
                    var loaded = ImageProcessingService.LoadImage(output);
                    if (loaded.PixelWidth != 512 || loaded.PixelHeight != 512) throw new InvalidDataException("Square dimensions");
                }
            });
            Check("4 borderless elements and transparent crop", () =>
            {
                var bitmap = Fixture(false);
                var regions = ImageProcessingService.DetectObjects(bitmap, 60, 40, .06, 18);
                if (regions.Count != 4) throw new InvalidDataException($"Expected 4 objects, got {regions.Count}");
                var output = ImageProcessingService.CreateTransparentSquare(bitmap, regions[0].Bounds, 256, 10, 16, TransparencyBackgroundSource.Borderless, null);
                if (ImageProcessingService.AnalyzeAlpha(output).Clear <= 0) throw new InvalidDataException("No transparency");
            });
            Check("long-image continuous width and total height", () =>
            {
                var bitmap = ImageProcessingService.LoadImage(source);
                var regions = CutPic.MainWindow.BuildLongRegions(width, height, 0, 3, 300, []);
                int total = 0;
                foreach (var region in regions)
                {
                    var crop = ImageProcessingService.CreateCrop(bitmap, region.Bounds);
                    if (crop.PixelWidth != width) throw new InvalidDataException("Lost width");
                    total += crop.PixelHeight;
                }
                if (total != height || regions.Count != 3) throw new InvalidDataException("Lost height");
            });
            Check("project save/load through main-window persistence", () =>
            {
                var window = new MainWindow();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(MainWindow).GetMethod("AddFiles", flags)!.Invoke(window, [new[] { source }]);
                string project = Path.Combine(directory, "test.cutpic");
                typeof(MainWindow).GetMethod("SaveProjectDocument", flags)!.Invoke(window, [project]);
                var saved = AtomicFileService.ReadJson<ProjectDocument>(project)!;
                if (saved.Tasks.Count != 1 || saved.Tasks[0].ImagePath != source) throw new InvalidDataException("Project task lost");
                typeof(MainWindow).GetMethod("LoadProjectDocument", flags)!.Invoke(window, [project, false]);
                typeof(MainWindow).GetMethod("SaveProjectDocument", flags)!.Invoke(window, [project]);
                if (!File.Exists(project + ".bak")) throw new InvalidDataException("No project backup");
                bool protectedSource = (bool)typeof(MainWindow).GetMethod("IsSourceImagePath", flags)!.Invoke(window, [source])!;
                if (!protectedSource) throw new InvalidDataException("Source overwrite guard failed");
            });
            Check("existing-file rename/skip and corrupted-image rejection", () =>
            {
                var method = typeof(MainWindow).GetMethod("ResolveOutputPath", BindingFlags.Static | BindingFlags.NonPublic)!;
                if ((string?)method.Invoke(null, [source, 0]) == source || method.Invoke(null, [source, 2]) is not null)
                    throw new InvalidDataException("Conflict protection failed");
                string corrupt = Path.Combine(directory, "corrupt.png");
                File.WriteAllText(corrupt, "invalid image");
                try { ImageProcessingService.LoadImage(corrupt); throw new InvalidOperationException("Corrupt image accepted"); }
                catch (Exception ex) when (ex is not InvalidOperationException) { }
            });
        }
        finally { Environment.SetEnvironmentVariable("CUTPIC_DATA_DIR", previous); }
        report.Add(failures == 0 ? "RESULT: PASSED (5 groups)" : $"RESULT: FAILED ({failures})");
        AtomicFileService.WriteAllText(reportPath, string.Join(Environment.NewLine, report));
        // Keep fixtures outside user data; async thumbnail tasks may still hold them until exit.
        return failures == 0 ? 0 : 1;
    }
}
