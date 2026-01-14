using BinarySerializer;
using BinarySerializer.Image;
using BinarySerializer.Ray1;
using BinarySerializer.Ray1.PC;

// Define constants
const string SerializerLogFilePath = "SerializerLog.txt";
const string MapFileName = "LevelMap.map";
const string TileSetFileNamePrefix = "TileSet";

void ShowHelpScreen()
{
    Console.WriteLine("Rayman 1 PCX Tool\n" +
                      "\n" +
                      "Usage:\n" +
                      "  -e <level-path> <output-path> | Exports the tileset and map from the level to the output path.\n" +
                      "                                  A separate tileset PCX file is exported per available palette.\n" +
                      "  -i <level-path> <input-path>  | Imports the tileset and map to the level from the input path.\n" +
                      "                                  The files have to be named the same as when exporting. Only the\n" +
                      "                                  first tileset PCX will be used for the tiles. The rest will only\n" +
                      "                                  be used to import alternative palettes.");
}

// Parse args
if (args.Length != 3)
{
    ShowHelpScreen();
    return;
}

string mode = args[0];
string levFilePath = args[1];
string exportedDir = args[2];

if (mode is not ("-e" or "-i") || !File.Exists(levFilePath))
{
    ShowHelpScreen();
    return;
}

// Create the context
SerializerSettings serializerSettings = new() { IgnoreCacheOnRead = true };
FileSerializerLogger serializerLogger = new(SerializerLogFilePath);
using Context context = new(String.Empty, settings: serializerSettings, serializerLogger: serializerLogger);

// TODO: Could support other versions, like Rayman Designer, by determining version from level file header
context.AddSettings(new Ray1Settings(Ray1EngineVersion.PC));

// Read the level
context.AddFile(new LinearFile(context, levFilePath));
LevelFile levFile = FileFactory.Read<LevelFile>(context, levFilePath);

// Export
if (mode == "-e")
{
    Directory.CreateDirectory(exportedDir);

    Console.WriteLine("Exporting tileset");

    // Get the raw image data for the tileset
    byte[][] scanLines = TileSetHelpers.PCBinaryTileSetToPCXScanLines(levFile);
    
    // Export as a separate pcx for each palette
    for (int i = 0; i < 3; i++)
    {
        // Convert the palette and set the first color to fully green like it usually is in Rayman Designer
        RGB888Color[] palette = ImageHelpers.ConvertPal666To888(levFile.MapInfo.Palettes[i]);
        palette[0] = new RGB888Color(0, 1, 0);

        // Create a pcx instance from the image data and palette
        PCX pcx = ImageHelpers.CreatePCX(scanLines, palette);

        // Save pcx file
        string pcxFilePath = Path.Combine(exportedDir, $"{TileSetFileNamePrefix}{i + 1}.pcx");
        context.AddFile(new LinearFile(context, pcxFilePath));
        FileFactory.Write<PCX>(context, pcxFilePath, pcx);
    }

    Console.WriteLine("Finished exporting tileset");

    Console.WriteLine("Exporting map");

    // Export map in the universal format (used by the Mapper)
    UniversalMap map = new()
    {
        Width = levFile.MapInfo.Width,
        Height = levFile.MapInfo.Height,
        Tiles = levFile.MapInfo.Blocks.Select(x => new UniversalMapBlock
        {
            TileIndex = x.TileIndex,
            BlockType = x.BlockType
        }).ToArray()
    };

    string mapFilePath = Path.Combine(exportedDir, MapFileName);
    context.AddFile(new LinearFile(context, mapFilePath));
    FileFactory.Write<UniversalMap>(context, mapFilePath, map);

    Console.WriteLine("Finished exporting map");
}
// Import
else if (mode == "-i")
{
    Console.WriteLine("Importing tileset");

    // Export as a separate pcx for each palette
    for (int i = 0; i < 3; i++)
    {
        string pcxFilePath = Path.Combine(exportedDir, $"{TileSetFileNamePrefix}{i + 1}.pcx");

        if (!File.Exists(pcxFilePath))
        {
            ConsoleHelpers.WriteWarning($"WARNING: File {pcxFilePath} not found");
            continue;
        }

        context.AddFile(new LinearFile(context, pcxFilePath));
        PCX pcx = FileFactory.Read<PCX>(context, pcxFilePath);

        // Only replace image data for the first file
        if (i == 0)
            levFile.TileSetNormal = TileSetHelpers.PCXScanLinesToPCBinaryTileSet(pcx.ScanLines);

        // Convert the palette and set the first color to fully black
        RGB666Color[] palette = ImageHelpers.ConvertPal888To666(pcx.VGAPalette);
        palette[0] = new RGB666Color(0, 0, 0);

        // Replace the palette
        levFile.MapInfo.Palettes[i] = palette;
    }

    Console.WriteLine("Finished importing tileset");

    Console.WriteLine("Importing map");

    string mapFilePath = Path.Combine(exportedDir, MapFileName);
    context.AddFile(new LinearFile(context, mapFilePath));
    UniversalMap map = FileFactory.Read<UniversalMap>(context, mapFilePath);

    levFile.MapInfo.Width = map.Width;
    levFile.MapInfo.Height = map.Height;
    levFile.MapInfo.Blocks = map.Tiles.Select(x => new Block
    {
        TileIndex = x.TileIndex,
        BlockType = x.BlockType,
    }).ToArray();

    Console.WriteLine("Finished importing map");

    // Update map tile render modes
    foreach (Block block in levFile.MapInfo.Blocks)
    {
        // NOTE: Opaque and fully transparent are flipped in the file data!
        if (block.TileIndex == 0)
        {
            block.RenderMode = Block.BlockRenderMode.Opaque;
        }
        else
        {
            uint offset = levFile.TileSetNormal.BlocksOffsetTable[block.TileIndex];
            bool isOpaque = offset < TileSetHelpers.OpaqueBlockDataLength * levFile.TileSetNormal.OpaqueBlocksCount;
            block.RenderMode = isOpaque ? Block.BlockRenderMode.FullyTransparent : Block.BlockRenderMode.Transparent;
        }
    }

    // Save the file
    FileFactory.Write<LevelFile>(context, levFilePath, levFile);
}
else
{
    ShowHelpScreen();
}

ConsoleHelpers.WriteSuccess("Complete");