using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;

namespace DarkColony.AssetExtractor;

public static class XbrzUpscaler
{
    /// <summary>
    /// Upscales a PNG image by the given factor using xBRZ algorithm.
    /// Falls back to bilinear interpolation if xbrzscale CLI is not available.
    /// </summary>
    public static void Upscale(string inputPath, string outputPath, int scaleFactor = 2)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Try xbrzscale CLI first (best quality)
        if (TryXbrzScaleCli(inputPath, outputPath, scaleFactor))
            return;

        // Fallback: use pixel-art-friendly nearest-neighbor 2x then smoothing
        UpscaleFallback(inputPath, outputPath, scaleFactor);
    }

    /// <summary>
    /// Batch upscale all PNG files in a directory.
    /// </summary>
    public static void UpscaleDirectory(string inputDir, string outputDir, int scaleFactor = 2)
    {
        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"Input directory not found: {inputDir}");
            return;
        }

        Directory.CreateDirectory(outputDir);

        var pngFiles = Directory.GetFiles(inputDir, "*.png");
        int total = pngFiles.Length;
        int done = 0;

        foreach (var inputFile in pngFiles)
        {
            string filename = Path.GetFileName(inputFile);
            string outputFile = Path.Combine(outputDir, filename);

            try
            {
                Upscale(inputFile, outputFile, scaleFactor);
                done++;
                Console.Write($"\rUpscaling: {done}/{total} ({filename})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nWarning: Failed to upscale {filename}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nUpscaling complete: {done}/{total} files processed.");
    }

    private static bool TryXbrzScaleCli(string inputPath, string outputPath, int scaleFactor)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xbrzscale",
                    Arguments = $"{scaleFactor} \"{inputPath}\" \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(30000); // 30 second timeout

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fallback upscaler: scale-2x algorithm (EPX/AdvMAME2x).
    /// This is a simple but effective pixel art scaling algorithm.
    /// For each pixel P with neighbors A(up), B(right), C(left), D(down):
    ///   1 = (C == A && C != D && A != B) ? A : P
    ///   2 = (A == B && A != C && B != D) ? B : P
    ///   3 = (D == C && D != B && C != A) ? C : P
    ///   4 = (B == D && B != A && D != C) ? D : P
    /// </summary>
    private static void UpscaleFallback(string inputPath, string outputPath, int scaleFactor)
    {
        using var input = Image.Load<Rgba32>(inputPath);

        if (scaleFactor == 2)
        {
            using var output = Scale2x(input);
            output.SaveAsPng(outputPath);
        }
        else
        {
            // For other scale factors, apply Scale2x repeatedly or use resize
            var current = input.Clone();

            int remaining = scaleFactor;
            while (remaining >= 2)
            {
                var scaled = Scale2x(current);
                current.Dispose();
                current = scaled;
                remaining /= 2;
            }

            // Handle non-power-of-2 with final resize
            if (remaining > 1 || scaleFactor != (int)Math.Pow(2, Math.Log2(scaleFactor)))
            {
                int targetW = input.Width * scaleFactor;
                int targetH = input.Height * scaleFactor;
                current.Mutate(ctx => ctx.Resize(targetW, targetH, KnownResamplers.NearestNeighbor));
            }

            current.SaveAsPng(outputPath);
            current.Dispose();
        }
    }

    private static Image<Rgba32> Scale2x(Image<Rgba32> input)
    {
        int w = input.Width;
        int h = input.Height;
        var output = new Image<Rgba32>(w * 2, h * 2);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = input[x, y];
                var a = y > 0 ? input[x, y - 1] : p;      // up
                var b = x < w - 1 ? input[x + 1, y] : p;  // right
                var c = x > 0 ? input[x - 1, y] : p;      // left
                var d = y < h - 1 ? input[x, y + 1] : p;  // down

                var p1 = (c.Equals(a) && !c.Equals(d) && !a.Equals(b)) ? a : p;
                var p2 = (a.Equals(b) && !a.Equals(c) && !b.Equals(d)) ? b : p;
                var p3 = (d.Equals(c) && !d.Equals(b) && !c.Equals(a)) ? c : p;
                var p4 = (b.Equals(d) && !b.Equals(a) && !d.Equals(c)) ? d : p;

                output[x * 2, y * 2] = p1;
                output[x * 2 + 1, y * 2] = p2;
                output[x * 2, y * 2 + 1] = p3;
                output[x * 2 + 1, y * 2 + 1] = p4;
            }
        }

        return output;
    }
}
