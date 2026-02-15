using System.IO.Compression;
using System.Text;

namespace DarkColony.AssetExtractor;

public class DcMapTile
{
    public ushort MainTileIndex { get; set; }
    public ushort OverlayTileIndex { get; set; }
    public bool IsFlippedH { get; set; }
    public bool IsFlippedV { get; set; }
    public bool IsImpassable { get; set; }
}

public class DcMap
{
    public int Width { get; set; }
    public int Height { get; set; }
    public DcMapTile[,] Tiles { get; set; } = new DcMapTile[0, 0];
}

public static class MapConverter
{
    /// <summary>
    /// Reads a Dark Colony .MAP file.
    /// </summary>
    public static DcMap ReadDcMap(string filePath)
    {
        return ReadDcMap(File.ReadAllBytes(filePath));
    }

    public static DcMap ReadDcMap(byte[] data)
    {
        var map = new DcMap();
        int offset = 0;

        // Width and height (uint32 LE)
        map.Width = (int)BitConverter.ToUInt32(data, offset);
        offset += 4;
        map.Height = (int)BitConverter.ToUInt32(data, offset);
        offset += 4;

        map.Tiles = new DcMapTile[map.Width, map.Height];

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (offset + 4 > data.Length)
                    break;

                ushort mainRaw = BitConverter.ToUInt16(data, offset);
                offset += 2;
                ushort overlayRaw = BitConverter.ToUInt16(data, offset);
                offset += 2;

                // Extract flags from high bits of main tile index
                bool flipH = (mainRaw & 0x8000) != 0;
                bool flipV = (mainRaw & 0x4000) != 0;
                bool impassable = (mainRaw & 0x2000) != 0;
                ushort tileIndex = (ushort)(mainRaw & 0x1FFF);

                map.Tiles[x, y] = new DcMapTile
                {
                    MainTileIndex = tileIndex,
                    OverlayTileIndex = overlayRaw,
                    IsFlippedH = flipH,
                    IsFlippedV = flipV,
                    IsImpassable = impassable
                };
            }
        }

        return map;
    }

    /// <summary>
    /// Converts a Dark Colony map to OpenRA .oramap directory format.
    /// Creates map.yaml and map.bin in the output directory.
    /// </summary>
    public static void ConvertToOpenRA(
        DcMap dcMap,
        string outputDir,
        string mapTitle = "Converted Map",
        string tileset = "MARS")
    {
        Directory.CreateDirectory(outputDir);

        // Write map.yaml
        var yaml = GenerateMapYaml(dcMap, mapTitle, tileset);
        File.WriteAllText(Path.Combine(outputDir, "map.yaml"), yaml);

        // Write map.bin (binary tile data)
        WriteMapBin(dcMap, Path.Combine(outputDir, "map.bin"));
    }

    private static string GenerateMapYaml(DcMap dcMap, string title, string tileset)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MapFormat: 11");
        sb.AppendLine("RequiresMod: dc");
        sb.AppendLine();
        sb.AppendLine($"Title: {title}");
        sb.AppendLine("Author: Dark Colony Asset Extractor");
        sb.AppendLine($"Tileset: {tileset}");
        sb.AppendLine($"MapSize: {dcMap.Width}, {dcMap.Height}");
        sb.AppendLine($"Bounds: 2, 2, {dcMap.Width - 4}, {dcMap.Height - 4}");
        sb.AppendLine("Visibility: Lobby");
        sb.AppendLine();
        sb.AppendLine("Players:");
        sb.AppendLine("\tNeutral:");
        sb.AppendLine("\t\tName: Neutral");
        sb.AppendLine("\t\tOwnsWorld: True");
        sb.AppendLine("\t\tNonCombatant: True");
        sb.AppendLine("\t\tFaction: humans");
        sb.AppendLine();
        sb.AppendLine("Actors:");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        return sb.ToString();
    }

    /// <summary>
    /// Writes OpenRA map.bin format.
    /// Format: tile data as sequential (ushort tileIndex, byte subIndex) per cell,
    /// row by row, left to right, top to bottom.
    /// </summary>
    private static void WriteMapBin(DcMap dcMap, string outputPath)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        for (int y = 0; y < dcMap.Height; y++)
        {
            for (int x = 0; x < dcMap.Width; x++)
            {
                var tile = dcMap.Tiles[x, y];

                // Map DC tile indices to our OpenRA tileset template IDs
                ushort templateId = MapTileToTemplate(tile);
                byte subIndex = 0;

                writer.Write(templateId);
                writer.Write(subIndex);
            }
        }
    }

    /// <summary>
    /// Maps a Dark Colony tile to an OpenRA tileset template ID.
    /// </summary>
    private static ushort MapTileToTemplate(DcMapTile tile)
    {
        if (tile.IsImpassable)
            return 2; // Impassable

        // Simple mapping based on main tile index ranges
        // This will need refinement based on actual DC tileset analysis
        return (ushort)(tile.MainTileIndex % 4); // Map to our 4 basic templates
    }
}
