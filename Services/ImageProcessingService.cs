using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CutPic.Models;
using System.IO;

namespace CutPic.Services;

public static class ImageProcessingService
{
    private readonly record struct Component(int MinX, int MinY, int MaxX, int MaxY, int Count);

    public static BitmapSource LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        if (source.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            return converted;
        }
        source.Freeze();
        return source;
    }

    public static IReadOnlyList<CropRegion> DetectCards(BitmapSource source)
    {
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        double borderLum = EstimateBorderLuminance(pixels, width, height, stride);
        double threshold = Math.Clamp(borderLum + 42, 55, 205);
        var light = new bool[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int p = y * stride + x * 4;
            light[y * width + x] = Luminance(pixels[p + 2], pixels[p + 1], pixels[p]) >= threshold;
        }

        var visited = new bool[light.Length];
        var queue = new int[light.Length];
        var components = new List<Component>();
        int minCount = Math.Max(500, width * height / 250);

        for (int index = 0; index < light.Length; index++)
        {
            if (!light[index] || visited[index]) continue;
            int head = 0, tail = 0;
            queue[tail++] = index;
            visited[index] = true;
            int minX = width, minY = height, maxX = 0, maxY = 0, count = 0;
            while (head < tail)
            {
                int current = queue[head++], y = current / width, x = current - y * width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); count++;
                TryAdd(current - 1, x > 0);
                TryAdd(current + 1, x + 1 < width);
                TryAdd(current - width, y > 0);
                TryAdd(current + width, y + 1 < height);

                void TryAdd(int next, bool valid)
                {
                    if (!valid || visited[next] || !light[next]) return;
                    visited[next] = true;
                    queue[tail++] = next;
                }
            }
            if (count >= minCount) components.Add(new(minX, minY, maxX, maxY, count));
        }

        var candidates = components
            .Where(c =>
            {
                int w = c.MaxX - c.MinX + 1, h = c.MaxY - c.MinY + 1;
                double fill = c.Count / (double)(w * h);
                return w >= width * .12 && h >= height * .12 &&
                       w <= width * .9 && h <= height * .9 &&
                       fill >= .48 && w / (double)h is >= .35 and <= 2.8;
            })
            .OrderBy(c => c.MinY + c.MaxY).ThenBy(c => c.MinX)
            .ToList();

        // Rounded corners can leave the component bounds one or two pixels inside the true card.
        var rects = candidates.Select(c => new Rect(
            Math.Max(0, c.MinX - 1), Math.Max(0, c.MinY - 1),
            Math.Min(width - Math.Max(0, c.MinX - 1), c.MaxX - c.MinX + 3),
            Math.Min(height - Math.Max(0, c.MinY - 1), c.MaxY - c.MinY + 3))).ToList();

        // Sort in reading order by grouping rows whose centers overlap.
        var ordered = new List<Rect>();
        foreach (var group in rects.GroupBy(r => (int)Math.Round(r.Top / Math.Max(20, MedianHeight(rects) * .45))))
            ordered.AddRange(group.OrderBy(r => r.Left));
        return ordered.Select((r, i) => new CropRegion { Number = i + 1, Bounds = r }).ToList();
    }

    public static IReadOnlyList<CropRegion> DetectObjects(BitmapSource source, double sensitivity = 60,
        int groupingDistance = 40, double minimumAreaPercent = .06, int padding = 18)
    {
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        Color background = EstimateOuterBackground(pixels, width, height, stride);
        double threshold = Math.Clamp(46 - sensitivity * .48, 7, 42);
        var foreground = new bool[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int p = y * stride + x * 4;
            double dr = pixels[p + 2] - background.R, dg = pixels[p + 1] - background.G, db = pixels[p] - background.B;
            // The maximum-channel term preserves very pale pink and blue watercolor edges.
            double delta = Math.Sqrt(dr * dr * .3 + dg * dg * .59 + db * db * .11);
            double maxDelta = Math.Max(Math.Abs(dr), Math.Max(Math.Abs(dg), Math.Abs(db)));
            foreground[y * width + x] = pixels[p + 3] > 10 && (delta >= threshold || maxDelta >= threshold * 1.35);
        }

        int radius = Math.Clamp(groupingDistance / 2, 1, Math.Max(1, Math.Min(width, height) / 12));
        int radiusY = Math.Max(radius, (int)Math.Round(radius * 1.25));
        var integral = new int[(width + 1) * (height + 1)];
        for (int y = 0; y < height; y++)
        {
            int rowSum = 0;
            for (int x = 0; x < width; x++)
            {
                if (foreground[y * width + x]) rowSum++;
                integral[(y + 1) * (width + 1) + x + 1] = integral[y * (width + 1) + x + 1] + rowSum;
            }
        }
        var expanded = new bool[foreground.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int left = Math.Max(0, x - radius), right = Math.Min(width - 1, x + radius);
            int top = Math.Max(0, y - radiusY), bottom = Math.Min(height - 1, y + radiusY);
            int sum = integral[(bottom + 1) * (width + 1) + right + 1] - integral[top * (width + 1) + right + 1]
                    - integral[(bottom + 1) * (width + 1) + left] + integral[top * (width + 1) + left];
            expanded[y * width + x] = sum > 0;
        }

        var visited = new bool[expanded.Length];
        var queue = new int[expanded.Length];
        var bounds = new List<(Rect Rect, int Count)>();
        int minimumPixels = Math.Max(12, (int)Math.Round(width * height * minimumAreaPercent / 100));
        for (int start = 0; start < expanded.Length; start++)
        {
            if (!expanded[start] || visited[start]) continue;
            int head = 0, tail = 0, originalCount = 0;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            queue[tail++] = start; visited[start] = true;
            while (head < tail)
            {
                int current = queue[head++], y = current / width, x = current - y * width;
                if (foreground[current])
                {
                    originalCount++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                Add(current - 1, x > 0); Add(current + 1, x + 1 < width);
                Add(current - width, y > 0); Add(current + width, y + 1 < height);
                void Add(int next, bool valid)
                {
                    if (!valid || visited[next] || !expanded[next]) return;
                    visited[next] = true; queue[tail++] = next;
                }
            }
            if (originalCount < minimumPixels || maxX < minX || maxY < minY) continue;
            double x0 = Math.Max(0, minX - padding), y0 = Math.Max(0, minY - padding);
            double x1 = Math.Min(width, maxX + padding + 1), y1 = Math.Min(height, maxY + padding + 1);
            bounds.Add((new Rect(x0, y0, x1 - x0, y1 - y0), originalCount));
        }

        // Remove tiny decorative islands unless they are already attached to a main object.
        var filtered = bounds.Where(v => v.Rect.Width >= width * .025 && v.Rect.Height >= height * .025)
            .Select(v => v.Rect).ToList();
        if (filtered.Count == 0) return [];
        double medianHeight = filtered.Select(r => r.Height).Order().ElementAt(filtered.Count / 2);
        var rows = new List<List<Rect>>();
        foreach (var rect in filtered.OrderBy(r => r.Top + r.Height / 2))
        {
            double center = rect.Top + rect.Height / 2;
            var row = rows.LastOrDefault();
            if (row is null || Math.Abs(center - row.Average(r => r.Top + r.Height / 2)) > medianHeight * .55) rows.Add([rect]);
            else row.Add(rect);
        }
        var ordered = rows.SelectMany(row => row.OrderBy(r => r.Left)).ToList();
        return ordered.Select((r, i) => new CropRegion { Number = i + 1, Bounds = r }).ToList();
    }

    public static BitmapSource CreateSquare(BitmapSource source, Rect requestedBounds, int outputSize,
        bool autoBackground, Color chosenBackground)
    {
        var bounds = ClampToImage(requestedBounds, source.PixelWidth, source.PixelHeight);
        int x = (int)Math.Round(bounds.X), y = (int)Math.Round(bounds.Y);
        int width = Math.Max(1, (int)Math.Round(bounds.Width));
        int height = Math.Max(1, (int)Math.Round(bounds.Height));
        int sourceStride = source.PixelWidth * 4;
        var all = new byte[sourceStride * source.PixelHeight];
        source.CopyPixels(all, sourceStride, 0);

        Color background = autoBackground
            ? EstimateCardBackground(all, source.PixelWidth, source.PixelHeight, sourceStride, x, y, width, height)
            : chosenBackground;

        var crop = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(all, (y + row) * sourceStride + x * 4, crop, row * width * 4, width * 4);
        RemoveOuterCornerBackground(crop, width, height, background);

        int side = Math.Max(width, height), squareStride = side * 4;
        var square = new byte[squareStride * side];
        Fill(square, background);
        int offsetX = (side - width) / 2, offsetY = (side - height) / 2;
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(crop, row * width * 4, square,
                (offsetY + row) * squareStride + offsetX * 4, width * 4);

        var bitmap = BitmapSource.Create(side, side, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, square, squareStride);
        if (outputSize <= 0 || outputSize == side) { bitmap.Freeze(); return bitmap; }
        var scaled = new TransformedBitmap(bitmap, new ScaleTransform(outputSize / (double)side, outputSize / (double)side));
        scaled.Freeze();
        return scaled;
    }

    public static BitmapSource CreateCrop(BitmapSource source, Rect requestedBounds)
    {
        var bounds = ClampToImage(requestedBounds, source.PixelWidth, source.PixelHeight);
        int x = (int)Math.Round(bounds.X), y = (int)Math.Round(bounds.Y);
        int width = Math.Max(1, Math.Min(source.PixelWidth - x, (int)Math.Round(bounds.Width)));
        int height = Math.Max(1, Math.Min(source.PixelHeight - y, (int)Math.Round(bounds.Height)));
        var crop = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
        crop.Freeze();
        return crop;
    }

    public static BitmapSource CreateTransparentSquare(BitmapSource source, Rect requestedBounds,
        int outputSize, int tolerance, int feather,
        TransparencyBackgroundSource backgroundSource = TransparencyBackgroundSource.Automatic,
        Color? sampledBackground = null)
    {
        var bounds = ClampToImage(requestedBounds, source.PixelWidth, source.PixelHeight);
        int x = (int)Math.Round(bounds.X), y = (int)Math.Round(bounds.Y);
        int width = Math.Max(1, Math.Min(source.PixelWidth - x, (int)Math.Round(bounds.Width)));
        int height = Math.Max(1, Math.Min(source.PixelHeight - y, (int)Math.Round(bounds.Height)));
        int sourceStride = source.PixelWidth * 4;
        var all = new byte[sourceStride * source.PixelHeight];
        source.CopyPixels(all, sourceStride, 0);
        Color outerBackground = EstimateOuterBackground(all, source.PixelWidth, source.PixelHeight, sourceStride);
        Color cardBackground = EstimateCardBackground(all, source.PixelWidth, source.PixelHeight, sourceStride,
            x, y, width, height);
        bool useCardBackground = backgroundSource == TransparencyBackgroundSource.Card ||
            backgroundSource == TransparencyBackgroundSource.Automatic &&
            ColorDistance(outerBackground, cardBackground) >= 30;
        Color background = sampledBackground ?? (useCardBackground ? cardBackground : outerBackground);

        var crop = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(all, (y + row) * sourceStride + x * 4, crop, row * width * 4, width * 4);
        if (useCardBackground)
            RemoveOuterCornerBackground(crop, width, height, background);

        tolerance = Math.Clamp(tolerance, 1, 100);
        feather = Math.Clamp(feather, 1, 100);
        // Card interiors are usually almost uniform. A conservative seed tolerance keeps
        // near-white products from becoming connected to that background through soft edges.
        int seedTolerance = useCardBackground ? Math.Min(tolerance, 4) : tolerance;
        int threshold = seedTolerance + feather;
        var connected = new bool[width * height];
        var queue = new int[connected.Length];
        int head = 0, tail = 0;

        bool IsBackgroundCandidate(int index)
        {
            int p = index * 4;
            int delta = Math.Max(Math.Abs(crop[p + 2] - background.R),
                Math.Max(Math.Abs(crop[p + 1] - background.G), Math.Abs(crop[p] - background.B)));
            return delta <= seedTolerance;
        }
        void Seed(int index)
        {
            if (connected[index] || !IsBackgroundCandidate(index)) return;
            connected[index] = true; queue[tail++] = index;
        }
        for (int xx = 0; xx < width; xx++) { Seed(xx); Seed((height - 1) * width + xx); }
        for (int yy = 0; yy < height; yy++) { Seed(yy * width); Seed(yy * width + width - 1); }
        while (head < tail)
        {
            int current = queue[head++], yy = current / width, xx = current - yy * width;
            Visit(current - 1, xx > 0); Visit(current + 1, xx + 1 < width);
            Visit(current - width, yy > 0); Visit(current + width, yy + 1 < height);
        }
        void Visit(int index, bool valid)
        {
            if (!valid || connected[index] || !IsBackgroundCandidate(index)) return;
            connected[index] = true; queue[tail++] = index;
        }

        var edgeLevel = new byte[connected.Length];
        head = 0;
        int featherRadius = Math.Clamp(feather / 4, 1, 8);
        while (head < tail)
        {
            int current = queue[head++], yy = current / width, xx = current - yy * width;
            int level = connected[current] ? 0 : edgeLevel[current];
            if (level >= featherRadius) continue;
            Expand(current - 1, xx > 0); Expand(current + 1, xx + 1 < width);
            Expand(current - width, yy > 0); Expand(current + width, yy + 1 < height);
            void Expand(int index, bool valid)
            {
                if (!valid || connected[index] || edgeLevel[index] != 0) return;
                int p = index * 4;
                int delta = Math.Max(Math.Abs(crop[p + 2] - background.R),
                    Math.Max(Math.Abs(crop[p + 1] - background.G), Math.Abs(crop[p] - background.B)));
                if (delta > threshold) return;
                edgeLevel[index] = (byte)(level + 1); queue[tail++] = index;
            }
        }

        for (int index = 0; index < connected.Length; index++)
        {
            int p = index * 4;
            if (connected[index])
            {
                crop[p] = crop[p + 1] = crop[p + 2] = crop[p + 3] = 0;
                continue;
            }
            if (edgeLevel[index] == 0) continue;
            int delta = Math.Max(Math.Abs(crop[p + 2] - background.R),
                Math.Max(Math.Abs(crop[p + 1] - background.G), Math.Abs(crop[p] - background.B)));
            double colorFactor = Math.Clamp((delta - seedTolerance) / (double)feather, 0, 1);
            double spatialFactor = edgeLevel[index] / (double)(featherRadius + 1);
            double alphaFactor = Math.Max(colorFactor, spatialFactor);
            byte originalAlpha = crop[p + 3];
            crop[p + 3] = (byte)Math.Round(originalAlpha * alphaFactor);
            if (alphaFactor <= .001)
            {
                crop[p] = crop[p + 1] = crop[p + 2] = 0;
            }
            else if (alphaFactor < .999)
            {
                crop[p + 2] = Unmatte(crop[p + 2], background.R, alphaFactor);
                crop[p + 1] = Unmatte(crop[p + 1], background.G, alphaFactor);
                crop[p] = Unmatte(crop[p], background.B, alphaFactor);
            }
        }

        int side = Math.Max(width, height), squareStride = side * 4;
        var square = new byte[squareStride * side];
        int offsetX = (side - width) / 2, offsetY = (side - height) / 2;
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(crop, row * width * 4, square,
                (offsetY + row) * squareStride + offsetX * 4, width * 4);
        var bitmap = BitmapSource.Create(side, side, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, square, squareStride);
        if (outputSize <= 0 || outputSize == side) { bitmap.Freeze(); return bitmap; }
        var scaled = new TransformedBitmap(bitmap, new ScaleTransform(outputSize / (double)side, outputSize / (double)side));
        scaled.Freeze(); return scaled;

        static byte Unmatte(byte composited, byte backgroundChannel, double alpha)
            => (byte)Math.Clamp(Math.Round((composited - backgroundChannel * (1 - alpha)) / alpha), 0, 255);
    }

    public static List<CropRegion> SuggestLongImageSlices(BitmapSource source, int targetHeight, int searchRange)
    {
        if (targetHeight < 40) throw new ArgumentException("目标高度不能小于 40px。");
        searchRange = Math.Clamp(searchRange, 10, Math.Max(10, targetHeight / 2));
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        Color background = EstimateOuterBackground(pixels, width, height, stride);
        int xStep = Math.Max(1, width / 500);
        var rowActivity = new double[height];
        for (int y = 0; y < height; y++)
        {
            int foreground = 0, samples = 0;
            for (int x = 0; x < width; x += xStep)
            {
                int p = y * stride + x * 4;
                int delta = Math.Max(Math.Abs(pixels[p + 2] - background.R),
                    Math.Max(Math.Abs(pixels[p + 1] - background.G), Math.Abs(pixels[p] - background.B)));
                if (delta > 15) foreground++;
                samples++;
            }
            rowActivity[y] = foreground / (double)Math.Max(1, samples);
        }

        var smooth = new double[height];
        const int band = 5;
        for (int y = 0; y < height; y++)
        {
            int top = Math.Max(0, y - band), bottom = Math.Min(height - 1, y + band);
            double sum = 0;
            for (int row = top; row <= bottom; row++) sum += rowActivity[row];
            smooth[y] = sum / (bottom - top + 1);
        }

        var boundaries = new List<int> { 0 };
        int current = 0;
        int minimumSegment = Math.Max(40, targetHeight / 3);
        while (height - current > targetHeight + minimumSegment)
        {
            int desired = current + targetHeight;
            int from = Math.Max(current + minimumSegment, desired - searchRange);
            int to = Math.Min(height - minimumSegment, desired + searchRange);
            if (from >= to) break;
            int best = from;
            double bestScore = double.MaxValue;
            for (int y = from; y <= to; y++)
            {
                double distancePenalty = Math.Abs(y - desired) / (double)searchRange * .025;
                double score = smooth[y] + distancePenalty;
                if (score < bestScore) { bestScore = score; best = y; }
            }
            boundaries.Add(best); current = best;
        }
        boundaries.Add(height);
        return boundaries.Zip(boundaries.Skip(1), (top, bottom) => new Rect(0, top, width, bottom - top))
            .Select((bounds, index) => new CropRegion { Number = index + 1, Bounds = bounds }).ToList();
    }

    public static void Save(BitmapSource image, string path, string format, int jpegQuality)
    {
        BitmapEncoder encoder = format.Equals("JPG", StringComparison.OrdinalIgnoreCase)
            ? new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) }
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static Rect ClampToImage(Rect r, int width, int height)
    {
        double x = Math.Clamp(r.X, 0, width - 1), y = Math.Clamp(r.Y, 0, height - 1);
        return new Rect(x, y, Math.Clamp(r.Width, 1, width - x), Math.Clamp(r.Height, 1, height - y));
    }

    private static double MedianHeight(List<Rect> rects) => rects.Count == 0 ? 1 : rects.Select(r => r.Height).Order().ElementAt(rects.Count / 2);

    private static double EstimateBorderLuminance(byte[] pixels, int width, int height, int stride)
    {
        var values = new List<double>();
        int step = Math.Max(1, Math.Min(width, height) / 120);
        for (int x = 0; x < width; x += step) { Add(x, 1); Add(x, height - 2); }
        for (int y = 0; y < height; y += step) { Add(1, y); Add(width - 2, y); }
        values.Sort();
        return values[values.Count / 2];
        void Add(int x, int y) { int p = y * stride + x * 4; values.Add(Luminance(pixels[p + 2], pixels[p + 1], pixels[p])); }
    }

    private static Color EstimateOuterBackground(byte[] pixels, int width, int height, int stride)
    {
        var rs = new List<byte>(); var gs = new List<byte>(); var bs = new List<byte>();
        int band = Math.Max(2, Math.Min(width, height) / 80);
        int step = Math.Max(1, Math.Min(width, height) / 160);
        for (int x = 0; x < width; x += step)
        for (int y = 0; y < height; y += Math.Max(1, height - band)) Add(x, y);
        for (int y = 0; y < height; y += step)
        for (int x = 0; x < width; x += Math.Max(1, width - band)) Add(x, y);
        return Color.FromRgb(Median(rs), Median(gs), Median(bs));
        void Add(int x, int y)
        {
            int p = y * stride + x * 4; bs.Add(pixels[p]); gs.Add(pixels[p + 1]); rs.Add(pixels[p + 2]);
        }
    }

    private static Color EstimateCardBackground(byte[] pixels, int imageWidth, int imageHeight, int stride,
        int x, int y, int width, int height)
    {
        var samples = new List<(byte B, byte G, byte R)>();
        int insetX = Math.Max(4, width / 18), insetY = Math.Max(4, height / 18);
        int bandX = Math.Max(2, width / 45), bandY = Math.Max(2, height / 45);
        int step = Math.Max(1, Math.Min(width, height) / 80);
        for (int xx = x + insetX; xx < x + width - insetX; xx += step)
        {
            AddBand(xx, y + insetY, true); AddBand(xx, y + height - insetY - 1, true);
        }
        for (int yy = y + insetY; yy < y + height - insetY; yy += step)
        {
            AddBand(x + insetX, yy, false); AddBand(x + width - insetX - 1, yy, false);
        }
        if (samples.Count == 0) return Colors.White;
        var bright = samples.OrderByDescending(s => Luminance(s.R, s.G, s.B)).Take(Math.Max(1, samples.Count * 3 / 4)).ToList();
        return Color.FromRgb(Median(bright.Select(v => v.R)), Median(bright.Select(v => v.G)), Median(bright.Select(v => v.B)));

        void AddBand(int xx, int yy, bool horizontal)
        {
            int count = horizontal ? bandY : bandX;
            for (int n = -count; n <= count; n++)
            {
                int sx = horizontal ? xx : xx + n, sy = horizontal ? yy + n : yy;
                if (sx < 0 || sx >= imageWidth || sy < 0 || sy >= imageHeight) continue;
                int p = sy * stride + sx * 4;
                samples.Add((pixels[p], pixels[p + 1], pixels[p + 2]));
            }
        }
    }

    private static void RemoveOuterCornerBackground(byte[] pixels, int width, int height, Color replacement)
    {
        int total = width * height;
        var seen = new bool[total];
        var queue = new int[total];
        int head = 0, tail = 0;
        double bgLum = Luminance(replacement.R, replacement.G, replacement.B);
        bool IsOuter(int index)
        {
            int p = index * 4;
            double lum = Luminance(pixels[p + 2], pixels[p + 1], pixels[p]);
            double distance = Math.Abs(pixels[p + 2] - replacement.R) + Math.Abs(pixels[p + 1] - replacement.G) + Math.Abs(pixels[p] - replacement.B);
            return lum < bgLum - 38 && distance > 75;
        }
        for (int x = 0; x < width; x++) { Seed(x); Seed((height - 1) * width + x); }
        for (int y = 0; y < height; y++) { Seed(y * width); Seed(y * width + width - 1); }
        while (head < tail)
        {
            int i = queue[head++], yy = i / width, xx = i - yy * width, p = i * 4;
            pixels[p] = replacement.B; pixels[p + 1] = replacement.G; pixels[p + 2] = replacement.R; pixels[p + 3] = 255;
            Visit(i - 1, xx > 0); Visit(i + 1, xx + 1 < width); Visit(i - width, yy > 0); Visit(i + width, yy + 1 < height);
        }
        // AI-generated cards commonly include a rounded one-pixel outline. Normalize only a
        // narrow perimeter band so the card blends into the newly padded square seamlessly.
        int edge = Math.Clamp((int)Math.Round(Math.Min(width, height) * .035), 3, 16);
        for (int yy = 0; yy < height; yy++)
        for (int xx = 0; xx < width; xx++)
        {
            if (xx >= edge && xx < width - edge && yy >= edge && yy < height - edge) continue;
            int p = (yy * width + xx) * 4;
            pixels[p] = replacement.B; pixels[p + 1] = replacement.G;
            pixels[p + 2] = replacement.R; pixels[p + 3] = 255;
        }
        void Seed(int i) { if (!seen[i] && IsOuter(i)) { seen[i] = true; queue[tail++] = i; } }
        void Visit(int i, bool valid) { if (valid && !seen[i] && IsOuter(i)) { seen[i] = true; queue[tail++] = i; } }
    }

    private static void Fill(byte[] data, Color color)
    {
        for (int p = 0; p < data.Length; p += 4)
        { data[p] = color.B; data[p + 1] = color.G; data[p + 2] = color.R; data[p + 3] = 255; }
    }

    private static byte Median(IEnumerable<byte> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? (byte)255 : sorted[sorted.Length / 2];
    }

    private static double Luminance(byte r, byte g, byte b) => .2126 * r + .7152 * g + .0722 * b;

    private static int ColorDistance(Color first, Color second) => Math.Max(Math.Abs(first.R - second.R),
        Math.Max(Math.Abs(first.G - second.G), Math.Abs(first.B - second.B)));

    public static Color SampleColor(BitmapSource source, int x, int y, int radius = 2)
    {
        x = Math.Clamp(x, 0, source.PixelWidth - 1);
        y = Math.Clamp(y, 0, source.PixelHeight - 1);
        radius = Math.Clamp(radius, 0, 12);
        int left = Math.Max(0, x - radius), top = Math.Max(0, y - radius);
        int width = Math.Min(source.PixelWidth - left, radius * 2 + 1);
        int height = Math.Min(source.PixelHeight - top, radius * 2 + 1);
        int stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(new Int32Rect(left, top, width, height), pixels, stride, 0);
        return Color.FromRgb(Median(Enumerable.Range(0, width * height).Select(i => pixels[i * 4 + 2])),
            Median(Enumerable.Range(0, width * height).Select(i => pixels[i * 4 + 1])),
            Median(Enumerable.Range(0, width * height).Select(i => pixels[i * 4])));
    }

    public static (double Clear, double Partial, double Opaque) AnalyzeAlpha(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        int clear = 0, partial = 0, opaque = 0;
        for (int p = 3; p < pixels.Length; p += 4)
        {
            if (pixels[p] <= 5) clear++;
            else if (pixels[p] >= 250) opaque++;
            else partial++;
        }
        double total = Math.Max(1, clear + partial + opaque);
        return (clear / total, partial / total, opaque / total);
    }
}
