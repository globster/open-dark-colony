namespace DarkColony.AssetExtractor;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("=== Dark Colony Asset Extractor ===");
        Console.WriteLine();

        string? gamePath = null;
        string? outputPath = null;
        string scaleStr = "1x";
        bool extractSprites = true;
        bool extractTerrain = true;
        bool extractMaps = true;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game-path" when i + 1 < args.Length:
                    gamePath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--scale" when i + 1 < args.Length:
                    scaleStr = args[++i];
                    break;
                case "--sprites-only":
                    extractTerrain = false;
                    extractMaps = false;
                    break;
                case "--terrain-only":
                    extractSprites = false;
                    extractMaps = false;
                    break;
                case "--maps-only":
                    extractSprites = false;
                    extractTerrain = false;
                    break;
                case "--collage" when i + 1 < args.Length:
                    return BuildCollage(args[++i]);
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
            }
        }

        if (string.IsNullOrEmpty(gamePath) || string.IsNullOrEmpty(outputPath))
        {
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(gamePath))
        {
            Console.WriteLine($"Error: Game path not found: {gamePath}");
            return 1;
        }

        int scaleFactor = scaleStr.TrimEnd('x', 'X') switch
        {
            "1" => 1,
            "2" => 2,
            "4" => 4,
            _ => 1
        };

        string assetsDir = Path.Combine(outputPath, "sequences", "assets");
        string assetsHdDir = Path.Combine(outputPath, "sequences", "assets-hd");
        string sequencesDir = Path.Combine(outputPath, "sequences");

        try
        {
            // Extract sprites
            if (extractSprites)
            {
                Console.WriteLine("--- Extracting sprites ---");
                ExtractSprites(gamePath, assetsDir, sequencesDir);
            }

            // Extract terrain
            if (extractTerrain)
            {
                Console.WriteLine("\n--- Extracting terrain ---");
                ExtractTerrain(gamePath, assetsDir);
            }

            // Extract maps
            if (extractMaps)
            {
                Console.WriteLine("\n--- Converting maps ---");
                ExtractMaps(gamePath, Path.Combine(outputPath, "maps"));
            }

            // HD upscaling
            if (scaleFactor > 1)
            {
                Console.WriteLine($"\n--- Upscaling sprites to {scaleFactor}x ---");
                XbrzUpscaler.UpscaleDirectory(assetsDir, assetsHdDir, scaleFactor);
            }

            Console.WriteLine("\n=== Extraction complete! ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static void ExtractSprites(string gamePath, string assetsDir, string sequencesDir)
    {
        Directory.CreateDirectory(assetsDir);

        // Find all SPR files
        var sprFiles = FindFiles(gamePath, "*.spr", "*.SPR");
        Console.WriteLine($"Found {sprFiles.Length} SPR files.");

        int extracted = 0;
        foreach (var sprFile in sprFiles)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(sprFile).ToLowerInvariant();
                string outputPng = Path.Combine(assetsDir, $"{baseName}.png");

                // Read SPR
                var spr = SprReader.Read(sprFile);

                if (spr.Frames.Length == 0)
                {
                    Console.WriteLine($"  Skipping {baseName}: no frames");
                    continue;
                }

                // Try to find matching FIN file
                FinFile? fin = null;
                string finPath = Path.ChangeExtension(sprFile, ".fin");
                if (!File.Exists(finPath))
                    finPath = Path.ChangeExtension(sprFile, ".FIN");

                if (File.Exists(finPath))
                {
                    try
                    {
                        fin = FinReader.Read(finPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Failed to read FIN for {baseName}: {ex.Message}");
                    }
                }

                // Write sprite sheet PNG
                PngSheetWriter.WriteSpriteSheet(spr, outputPng, fin);

                // Generate sequences YAML
                string pngRelative = $"assets/{baseName}.png";
                string yaml = PngSheetWriter.GenerateSequencesYaml(baseName, pngRelative, spr, fin);

                // Append to appropriate sequences file
                string seqFile = Path.Combine(sequencesDir, "extracted.yaml");
                File.AppendAllText(seqFile, yaml + "\n");

                extracted++;
                Console.Write($"\rExtracted: {extracted}/{sprFiles.Length} ({baseName})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Error processing {sprFile}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nSprite extraction complete: {extracted}/{sprFiles.Length} files.");
    }

    static void ExtractTerrain(string gamePath, string assetsDir)
    {
        Directory.CreateDirectory(assetsDir);

        var btsFiles = FindFiles(gamePath, "*.bts", "*.BTS");
        Console.WriteLine($"Found {btsFiles.Length} BTS files.");

        int extracted = 0;
        foreach (var btsFile in btsFiles)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(btsFile).ToLowerInvariant();
                string outputPng = Path.Combine(assetsDir, $"terrain_{baseName}.png");

                var bts = BtsReader.Read(btsFile);

                if (bts.Tiles.Length == 0)
                {
                    Console.WriteLine($"  Skipping {baseName}: no tiles");
                    continue;
                }

                PngSheetWriter.WriteTileSheet(bts, outputPng);

                extracted++;
                Console.Write($"\rExtracted terrain: {extracted}/{btsFiles.Length} ({baseName})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Error processing {btsFile}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nTerrain extraction complete: {extracted}/{btsFiles.Length} files.");
    }

    static void ExtractMaps(string gamePath, string mapsDir)
    {
        Directory.CreateDirectory(mapsDir);

        var mapFiles = FindFiles(gamePath, "*.map", "*.MAP");
        Console.WriteLine($"Found {mapFiles.Length} MAP files.");

        int converted = 0;
        foreach (var mapFile in mapFiles)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(mapFile).ToLowerInvariant();
                string outputDir = Path.Combine(mapsDir, $"dc-{baseName}");

                var dcMap = MapConverter.ReadDcMap(mapFile);

                if (dcMap.Width == 0 || dcMap.Height == 0)
                {
                    Console.WriteLine($"  Skipping {baseName}: empty map");
                    continue;
                }

                MapConverter.ConvertToOpenRA(dcMap, outputDir, $"Dark Colony - {baseName}");

                converted++;
                Console.Write($"\rConverted maps: {converted}/{mapFiles.Length} ({baseName})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Error converting {mapFile}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nMap conversion complete: {converted}/{mapFiles.Length} files.");
    }

    static string[] FindFiles(string directory, params string[] patterns)
    {
        var files = new List<string>();
        foreach (var pattern in patterns)
        {
            files.AddRange(Directory.GetFiles(directory, pattern, SearchOption.AllDirectories));
        }
        return files.Distinct().ToArray();
    }

    static int BuildCollage(string assetsDir)
    {
        // Key units from both factions + buildings + effects + creatures
        var units = new[]
        {
            // Human faction
            "troop", "trooper1", "trooper2", "sarg", "reap", "arty", "expl",
            "hcom", "hcar", "engi", "scou", "barr", "cyborg", "towr", "turr",
            // Taar faction
            "gray", "atril", "slom", "ortu", "zisp", "xeno", "slug",
            "alien1", "bees", "psych", "sauc", "spid", "tong", "syth",
            // Buildings
            "buildng", "cent", "fact", "fuel", "hubu",
            // Effects & misc
            "drop", "beac", "vent", "cryo", "nuke", "sonic", "fire",
            // Vehicles
            "truk", "aird", "avii", "camm", "brit",
        };

        string outputPath = Path.Combine(assetsDir, "..", "collage.png");
        CollageBuilder.BuildCollage(assetsDir, outputPath, units, columns: 8, cellSize: 96, padding: 6);
        return 0;
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: DarkColony.AssetExtractor [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --game-path <path>   Path to original Dark Colony game directory");
        Console.WriteLine("  --output <path>       Output path (mod directory, e.g. mods/dc)");
        Console.WriteLine("  --scale <1x|2x|4x>   Upscale factor (default: 1x)");
        Console.WriteLine("  --sprites-only        Only extract sprite files");
        Console.WriteLine("  --terrain-only        Only extract terrain tiles");
        Console.WriteLine("  --maps-only           Only convert map files");
        Console.WriteLine("  -h, --help           Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- --game-path \"/path/to/DarkColony\" --output \"../../mods/dc\" --scale 1x");
        Console.WriteLine("  dotnet run -- --game-path \"/path/to/DarkColony\" --output \"../../mods/dc\" --scale 2x");
    }
}
