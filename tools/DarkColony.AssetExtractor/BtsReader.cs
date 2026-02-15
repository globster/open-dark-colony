using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DarkColony.AssetExtractor;

public class BtsTile
{
    public uint Index { get; set; }
    public byte[] PixelData { get; set; } = new byte[1024]; // 32x32 palette indices
}

public class BtsFile
{
    public Rgba32[] Palette { get; set; } = new Rgba32[256];
    public BtsTile[] Tiles { get; set; } = Array.Empty<BtsTile>();
}

public static class BtsReader
{
    public const int TileSize = 32;
    public const int TilePixels = TileSize * TileSize; // 1024
    public const int TileBlockSize = 4 + TilePixels;    // 1028

    public static BtsFile Read(string filePath)
    {
        return Read(File.ReadAllBytes(filePath));
    }

    public static BtsFile Read(byte[] data)
    {
        var bts = new BtsFile();

        // Read palette (same format as SPR: bytes 8-775)
        bts.Palette = new Rgba32[256];
        for (int i = 0; i < 256; i++)
        {
            int offset = 8 + i * 3;
            if (offset + 2 >= data.Length)
                break;

            byte r = (byte)Math.Min(255, data[offset] * 4 + 3);
            byte g = (byte)Math.Min(255, data[offset + 1] * 4 + 3);
            byte b = (byte)Math.Min(255, data[offset + 2] * 4 + 3);
            bts.Palette[i] = new Rgba32(r, g, b, 255);
        }

        // Tile data starts after palette (offset 776)
        int tileDataStart = 776;
        int remainingBytes = data.Length - tileDataStart;
        int tileCount = remainingBytes / TileBlockSize;

        bts.Tiles = new BtsTile[tileCount];

        for (int i = 0; i < tileCount; i++)
        {
            int blockOffset = tileDataStart + i * TileBlockSize;
            var tile = new BtsTile
            {
                Index = BitConverter.ToUInt32(data, blockOffset)
            };

            Array.Copy(data, blockOffset + 4, tile.PixelData, 0, TilePixels);
            bts.Tiles[i] = tile;
        }

        return bts;
    }

    /// <summary>
    /// Renders a single tile to an RGBA image.
    /// </summary>
    public static Image<Rgba32> RenderTile(BtsFile bts, int tileIndex)
    {
        var tile = bts.Tiles[tileIndex];
        var image = new Image<Rgba32>(TileSize, TileSize);

        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int idx = y * TileSize + x;
                byte paletteIndex = tile.PixelData[idx];
                image[x, y] = bts.Palette[paletteIndex];
            }
        }

        return image;
    }
}
