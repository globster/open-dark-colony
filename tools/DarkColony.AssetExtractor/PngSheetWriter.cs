using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace DarkColony.AssetExtractor;

public static class PngSheetWriter
{
    /// <summary>
    /// Writes all frames of an SPR file to a horizontal-strip PNG.
    /// Frames are laid out left to right. If FIN data is available,
    /// frames are grouped by animation, then by facing.
    /// </summary>
    public static void WriteSpriteSheet(
        SprFile spr,
        string outputPngPath,
        FinFile? fin = null)
    {
        if (spr.Frames.Length == 0)
            return;

        // Calculate sheet dimensions
        int maxWidth = spr.Frames.Max(f => f.Width);
        int maxHeight = spr.Frames.Max(f => f.Height);
        int frameCount = spr.Frames.Length;

        // Horizontal strip: all frames in a row
        int sheetWidth = maxWidth * frameCount;
        int sheetHeight = maxHeight;

        using var sheet = new Image<Rgba32>(sheetWidth, sheetHeight);

        // Fill with transparent
        sheet.Mutate(ctx => ctx.BackgroundColor(Color.Transparent));

        // Draw each frame
        for (int i = 0; i < frameCount; i++)
        {
            var frame = spr.Frames[i];
            using var frameImage = SprReader.RenderFrame(spr, i);

            // Center each frame within its cell, accounting for displacement
            int cellX = i * maxWidth;
            int offsetX = (maxWidth - frame.Width) / 2 + frame.DisplacementX;
            int offsetY = (maxHeight - frame.Height) / 2 + frame.DisplacementY;

            // Clamp offsets
            offsetX = Math.Max(0, Math.Min(offsetX, maxWidth - frame.Width));
            offsetY = Math.Max(0, Math.Min(offsetY, maxHeight - frame.Height));

            var point = new Point(cellX + offsetX, offsetY);
            sheet.Mutate(ctx => ctx.DrawImage(frameImage, point, 1f));
        }

        // Ensure output directory exists
        var dir = Path.GetDirectoryName(outputPngPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        sheet.SaveAsPng(outputPngPath);
    }

    /// <summary>
    /// Generates OpenRA sequences YAML for a unit based on FIN animation data.
    /// </summary>
    public static string GenerateSequencesYaml(
        string actorName,
        string pngFilename,
        SprFile spr,
        FinFile? fin,
        int facings = 8)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{actorName}:");

        if (fin != null && fin.Animations.Count > 0)
        {
            foreach (var anim in fin.Animations)
            {
                string seqName = NormalizeAnimationName(anim.Name);
                sb.AppendLine($"\t{seqName}:");
                sb.AppendLine($"\t\tFilename: {pngFilename}");
                sb.AppendLine($"\t\tStart: {anim.StartFrame}");
                sb.AppendLine($"\t\tLength: {anim.FrameCount}");

                // Only apply facings to movement/idle/attack animations
                if (seqName is "idle" or "walk" or "move" or "attack" or "harvest")
                    sb.AppendLine($"\t\tFacings: {facings}");
            }
        }
        else
        {
            // Fallback: single idle sequence with all frames
            sb.AppendLine("\tidle:");
            sb.AppendLine($"\t\tFilename: {pngFilename}");
            sb.AppendLine($"\t\tStart: 0");
            sb.AppendLine($"\t\tLength: {spr.Frames.Length}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates OpenRA terrain tile sequences YAML.
    /// </summary>
    public static string GenerateTerrainSequencesYaml(
        string tilesetName,
        string pngFilename,
        int tileCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{tilesetName}:");
        sb.AppendLine("\tidle:");
        sb.AppendLine($"\t\tFilename: {pngFilename}");
        sb.AppendLine($"\t\tStart: 0");
        sb.AppendLine($"\t\tLength: {tileCount}");
        return sb.ToString();
    }

    /// <summary>
    /// Writes a BTS tileset to a horizontal-strip PNG.
    /// </summary>
    public static void WriteTileSheet(
        BtsFile bts,
        string outputPngPath)
    {
        if (bts.Tiles.Length == 0)
            return;

        int tileCount = bts.Tiles.Length;
        int sheetWidth = BtsReader.TileSize * tileCount;
        int sheetHeight = BtsReader.TileSize;

        using var sheet = new Image<Rgba32>(sheetWidth, sheetHeight);

        for (int i = 0; i < tileCount; i++)
        {
            using var tileImage = BtsReader.RenderTile(bts, i);
            var point = new Point(i * BtsReader.TileSize, 0);
            sheet.Mutate(ctx => ctx.DrawImage(tileImage, point, 1f));
        }

        var dir = Path.GetDirectoryName(outputPngPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        sheet.SaveAsPng(outputPngPath);
    }

    private static string NormalizeAnimationName(string name)
    {
        // Map Dark Colony animation names to OpenRA sequence names
        var normalized = name.ToLowerInvariant().Trim();
        return normalized switch
        {
            "stand" or "idle" or "still" => "idle",
            "walk" or "run" or "move" => "walk",
            "attack" or "fire" or "shoot" => "attack",
            "die" or "death" or "dead" => "die",
            "harvest" or "gather" or "mine" => "harvest",
            _ => normalized
        };
    }
}
