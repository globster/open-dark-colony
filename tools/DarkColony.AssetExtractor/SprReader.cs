using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DarkColony.AssetExtractor;

public class SprFrame
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int DisplacementX { get; set; }
    public int DisplacementY { get; set; }
    public byte[] PixelData { get; set; } = Array.Empty<byte>(); // palette indices
}

public class SprFile
{
    public bool IsCompressed { get; set; }
    public Rgba32[] Palette { get; set; } = new Rgba32[256];
    public SprFrame[] Frames { get; set; } = Array.Empty<SprFrame>();

    // Team color palette indices (138-143)
    public static readonly int[] TeamColorIndices = { 138, 139, 140, 141, 142, 143 };
}

public static class SprReader
{
    public static SprFile Read(string filePath)
    {
        return Read(File.ReadAllBytes(filePath));
    }

    public static SprFile Read(byte[] data)
    {
        var spr = new SprFile();

        // Byte 0: compression flag
        spr.IsCompressed = data[0] == 129;

        // Bytes 2-3: frame count (little-endian)
        int frameCount = BitConverter.ToUInt16(data, 2);

        // Bytes 8-775: palette (256 entries, 3 bytes each)
        spr.Palette = new Rgba32[256];
        for (int i = 0; i < 256; i++)
        {
            int offset = 8 + i * 3;
            byte r = (byte)Math.Min(255, data[offset] * 4 + 3);
            byte g = (byte)Math.Min(255, data[offset + 1] * 4 + 3);
            byte b = (byte)Math.Min(255, data[offset + 2] * 4 + 3);
            spr.Palette[i] = new Rgba32(r, g, b, (byte)(i == 0 ? 0 : 255)); // index 0 = transparent
        }

        // Frame headers start at offset 776 (8 + 768)
        int headerOffset = 776;
        spr.Frames = new SprFrame[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            var frame = new SprFrame
            {
                Width = BitConverter.ToUInt16(data, headerOffset),
                Height = BitConverter.ToUInt16(data, headerOffset + 2),
                DisplacementX = BitConverter.ToInt16(data, headerOffset + 4),
                DisplacementY = BitConverter.ToInt16(data, headerOffset + 6)
            };
            spr.Frames[i] = frame;
            headerOffset += 8;
        }

        // Pixel data follows all frame headers
        int pixelOffset = headerOffset;

        for (int i = 0; i < frameCount; i++)
        {
            var frame = spr.Frames[i];
            int totalPixels = frame.Width * frame.Height;
            frame.PixelData = new byte[totalPixels];

            if (!spr.IsCompressed)
            {
                // Uncompressed: direct palette indices
                if (pixelOffset + totalPixels <= data.Length)
                {
                    Array.Copy(data, pixelOffset, frame.PixelData, 0, totalPixels);
                    pixelOffset += totalPixels;
                }
            }
            else
            {
                // Compressed: RLE decoding
                int pixelPos = 0;
                while (pixelPos < totalPixels && pixelOffset < data.Length)
                {
                    byte control = data[pixelOffset++];

                    if (control < 128)
                    {
                        // Next (control + 1) bytes are raw palette indices
                        int count = control + 1;
                        for (int j = 0; j < count && pixelPos < totalPixels && pixelOffset < data.Length; j++)
                        {
                            frame.PixelData[pixelPos++] = data[pixelOffset++];
                        }
                    }
                    else
                    {
                        // Skip (256 - control) pixels as transparent (index 0)
                        int count = 256 - control;
                        for (int j = 0; j < count && pixelPos < totalPixels; j++)
                        {
                            frame.PixelData[pixelPos++] = 0; // transparent
                        }
                    }
                }
            }
        }

        return spr;
    }

    /// <summary>
    /// Renders a single frame to an RGBA image.
    /// </summary>
    public static Image<Rgba32> RenderFrame(SprFile spr, int frameIndex)
    {
        var frame = spr.Frames[frameIndex];
        var image = new Image<Rgba32>(frame.Width, frame.Height);

        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                int idx = y * frame.Width + x;
                byte paletteIndex = frame.PixelData[idx];
                image[x, y] = spr.Palette[paletteIndex];
            }
        }

        return image;
    }
}
