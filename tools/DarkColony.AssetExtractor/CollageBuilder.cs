using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DarkColony.AssetExtractor;

public static class CollageBuilder
{
    /// <summary>
    /// Creates a collage image from selected unit sprite sheets.
    /// Picks the first frame from each sprite sheet and arranges them in a grid.
    /// </summary>
    public static void BuildCollage(string assetsDir, string outputPath, string[] unitNames, int columns = 6, int cellSize = 128, int padding = 8)
    {
        var images = new List<(string name, Image<Rgba32> img)>();

        foreach (var name in unitNames)
        {
            string path = Path.Combine(assetsDir, $"{name}.png");
            if (!File.Exists(path))
                continue;

            try
            {
                var sheet = Image.Load<Rgba32>(path);
                // Extract first frame: estimate frame width from sheet height (square-ish frames)
                int frameW = Math.Min(sheet.Height, sheet.Width);
                if (frameW <= 0) continue;

                var frame = sheet.Clone(ctx => ctx.Crop(new Rectangle(0, 0, frameW, sheet.Height)));
                images.Add((name, frame));
            }
            catch
            {
                // Skip problematic files
            }
        }

        if (images.Count == 0)
        {
            Console.WriteLine("No valid unit images found for collage.");
            return;
        }

        int rows = (images.Count + columns - 1) / columns;
        int totalW = columns * (cellSize + padding) + padding;
        int totalH = rows * (cellSize + padding) + padding + 40; // extra for title

        using var collage = new Image<Rgba32>(totalW, totalH);

        // Dark background
        collage.Mutate(ctx => ctx.BackgroundColor(new Rgba32(20, 12, 8, 255)));

        for (int i = 0; i < images.Count; i++)
        {
            var (name, img) = images[i];
            int col = i % columns;
            int row = i / columns;

            // Scale frame to fit cell while preserving aspect ratio
            float scale = Math.Min((float)cellSize / img.Width, (float)cellSize / img.Height);
            int scaledW = Math.Max(1, (int)(img.Width * scale));
            int scaledH = Math.Max(1, (int)(img.Height * scale));

            using var resized = img.Clone(ctx => ctx.Resize(scaledW, scaledH, KnownResamplers.NearestNeighbor));

            int x = padding + col * (cellSize + padding) + (cellSize - scaledW) / 2;
            int y = padding + 40 + row * (cellSize + padding) + (cellSize - scaledH) / 2;

            collage.Mutate(ctx => ctx.DrawImage(resized, new Point(x, y), 1f));
        }

        // Clean up loaded images
        foreach (var (_, img) in images)
            img.Dispose();

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        collage.SaveAsPng(outputPath);
        Console.WriteLine($"Collage saved to: {outputPath} ({images.Count} units, {totalW}x{totalH}px)");
    }
}
