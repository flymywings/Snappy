using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: IconTool <source.png|source.xaml> <output-directory>");
        Directory.CreateDirectory(args[1]);
        var raster = !Path.GetExtension(args[0]).Equals(".xaml", StringComparison.OrdinalIgnoreCase);
        ImageSource drawing;
        Int32Rect? contentBounds = null;
        using (var source = File.OpenRead(args[0]))
        {
            if (!raster) drawing = (DrawingImage)XamlReader.Load(source);
            else
            {
                var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                contentBounds = FindContent(bitmap);
                drawing = new CroppedBitmap(bitmap, contentBounds.Value);
                Console.WriteLine($"Raster content bounds: {contentBounds.Value}. Uniform scaling; source pixels retained.");
            }
        }
        drawing.Freeze();
        var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var payloads = new List<byte[]>();
        foreach (var size in sizes)
        {
            var visual = new DrawingVisual();
            var padding = raster ? Math.Max(0.5, size * 0.02) : 0;
            var scale = (size - 2 * padding) / Math.Max(drawing.Width, drawing.Height);
            var width = drawing.Width * scale; var height = drawing.Height * scale;
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (var dc = visual.RenderOpen()) dc.DrawImage(drawing, new Rect((size - width) / 2, (size - height) / 2, width, height));
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var bytes = new MemoryStream(); png.Save(bytes); payloads.Add(bytes.ToArray());
            File.WriteAllBytes(Path.Combine(args[1], $"Snappy-{size}.png"), bytes.ToArray());
        }
        using var stream = File.Create(Path.Combine(args[1], "Snappy.ico")); using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        var offset = 6 + sizes.Length * 16;
        for (var i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(payloads[i].Length); writer.Write(offset); offset += payloads[i].Length;
        }
        foreach (var payload in payloads) writer.Write(payload);
        File.WriteAllText(Path.Combine(args[1], "Snappy-export.json"), JsonSerializer.Serialize(new
        {
            Source = Path.GetFileName(args[0]), SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
            SourceType = raster ? "approved-raster" : "vector", ContentBounds = contentBounds?.ToString(),
            UniformScaling = true, Sizes = sizes
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Created {sizes.Length}-size ICO and transparent PNGs from {(raster ? "approved raster" : "vector")} source.");
    }
    private static Int32Rect FindContent(BitmapSource image)
    {
        // Locate the connected artwork, ignoring detached generation specks and empty margins.
        // Only the export viewport changes; the approved master file is never rewritten.
        var width = image.PixelWidth; var height = image.PixelHeight;
        var pixels = new byte[width * height * 4]; image.CopyPixels(pixels, width * 4, 0);
        var visited = new bool[width * height]; var queue = new int[visited.Length];
        var largest = 0; var best = new Int32Rect();
        for (var index = 0; index < visited.Length; index++)
        {
            if (visited[index] || pixels[index * 4 + 3] <= 16) continue;
            var head = 0; var tail = 0; var minX = width; var minY = height; var maxX = 0; var maxY = 0;
            void Add(int next)
            {
                if (visited[next] || pixels[next * 4 + 3] <= 16) return;
                visited[next] = true; queue[tail++] = next;
            }
            Add(index);
            while (head < tail)
            {
                var current = queue[head++]; var x = current % width; var y = current / width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                if (x > 0) Add(current - 1); if (x + 1 < width) Add(current + 1);
                if (y > 0) Add(current - width); if (y + 1 < height) Add(current + width);
            }
            if (tail <= largest) continue;
            largest = tail; best = new(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        if (largest == 0) throw new InvalidDataException("Source artwork is fully transparent.");
        var padding = (int)Math.Ceiling(Math.Max(width, height) * 0.008);
        var left = Math.Max(0, best.X - padding); var top = Math.Max(0, best.Y - padding);
        return new(left, top, Math.Min(width, best.X + best.Width + padding) - left,
            Math.Min(height, best.Y + best.Height + padding) - top);
    }
}
