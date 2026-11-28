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
            Check("two-task cross-source protection and actual rename/skip/overwrite writes", () =>
            {
                string a = Path.Combine(directory, "task-a.png"), b = Path.Combine(directory, "task-b.png");
                File.Copy(source, a); File.Copy(source, b);
                var window = new MainWindow();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(MainWindow).GetMethod("AddFiles", flags)!.Invoke(window, [new[] { a, b }]);
                var tasks = (System.Collections.ICollection)typeof(MainWindow).GetField("_tasks", flags)!.GetValue(window)!;
                if (tasks.Count != 2) throw new InvalidDataException("Regression requires two loaded tasks");
                var save = typeof(MainWindow).GetMethod("SaveExportOutput", flags)!;
                var output = ImageProcessingService.CreateSquare(Fixture(false), new Rect(0,0,600,800), 64, false, Colors.Red);
                string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                string originalA = Hash(a), originalB = Hash(b);
                bool Write(string path, int policy) => (bool)save.Invoke(window, [output, path, policy, "PNG", 92])!;
                if (!Write(b, 0) || !File.Exists(Path.Combine(directory, "task-b (1).png"))) throw new InvalidDataException("Rename failed");
                if (Write(b, 2)) throw new InvalidDataException("Skip wrote to source");
                try { Write(b, 1); throw new InvalidDataException("Cross-task overwrite accepted"); }
                catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
                if (Hash(a) != originalA || Hash(b) != originalB) throw new InvalidDataException("Source changed");
                string ordinary = Path.Combine(directory, "ordinary.png"); File.Copy(source, ordinary);
                if (Write(ordinary, 2) || Hash(ordinary) != originalA) throw new InvalidDataException("Ordinary skip failed");
                if (!Write(ordinary, 0) || Hash(ordinary) != originalA || !File.Exists(Path.Combine(directory, "ordinary (1).png"))) throw new InvalidDataException("Ordinary rename failed");
                if (!Write(ordinary, 1) || Hash(ordinary) == originalA || ImageProcessingService.LoadImage(ordinary).PixelWidth != 64) throw new InvalidDataException("Ordinary overwrite failed");
            });
        }
        finally { Environment.SetEnvironmentVariable("CUTPIC_DATA_DIR", previous); }
        report.Add(failures == 0 ? "RESULT: PASSED (6 groups)" : $"RESULT: FAILED ({failures})");
        AtomicFileService.WriteAllText(reportPath, string.Join(Environment.NewLine, report));
        // Keep fixtures outside user data; async thumbnail tasks may still hold them until exit.
        return failures == 0 ? 0 : 1;
    }
}
