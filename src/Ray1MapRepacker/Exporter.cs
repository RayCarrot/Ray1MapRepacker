using BinarySerializer;
using BinarySerializer.Image;
using BinarySerializer.Ray1;
using BinarySerializer.Ray1.PC;

namespace Ray1MapRepacker;

public class Exporter(Context context)
{
    /// <summary>
    /// Exports the tileset PCX and map from the level to the output path.
    /// A separate tileset PCX file is exported per available palette
    /// </summary>
    /// <param name="levFilePath"> Path to the lev file that should be exported. </param>
    /// <param name="mapFileDir"> Path to the map dir, the lev data should be exported to. </param>
    /// <param name="mapFileName"> Name of the map file, the lev data is exported to. </param>
    /// <param name="tilesetNamePrefix"> Name prefix for the created tileset file. </param>
    public void ExportLevel(string levFilePath, string mapFileDir, string mapFileName, string tilesetNamePrefix)
    {
        Console.WriteLine($"Starting export process for {levFilePath}");
    
        LevelFile levFile = ContextHelper.ReadLevelFile(context, levFilePath);
        ExportTileSet(levFile, mapFileDir, tilesetNamePrefix);
        ExportAndSaveMap(levFile, mapFileDir, mapFileName, []);
    
        Console.WriteLine($"Finished export process for {levFilePath}");
    }
    
    public void ExportLevelForceUsingTileSet(string levFilePath, string mapFileDir, string mapFileName, string tileSetPathPCX)
    {
        Console.WriteLine($"Starting export process for {levFilePath}");
        
        context.AddFile(new LinearFile(context, tileSetPathPCX));
        PCX pcx = FileFactory.Read<PCX>(context, tileSetPathPCX);
        LevelFile levFile = ContextHelper.ReadLevelFile(context, levFilePath);

        ushort[][] tileSetIndexMaps = TileSetHelpers.CreateTileSetIndexMaps(levFile, pcx);
        ExportAndSaveMap(levFile, mapFileDir, mapFileName, tileSetIndexMaps);
    
        Console.WriteLine($"Finished export process for {levFilePath}");
    }
    
    private void ExportTileSet(LevelFile levFile, string mapFileDir, string tilesetNamePrefix)
    {
        Directory.CreateDirectory(mapFileDir);

        Console.WriteLine("Exporting tileset");

        // Get the raw image data for the tileset
        byte[][] scanLines = TileSetHelpers.PCBinaryTileSetToPCXScanLines(levFile);
    
        // Export as a separate pcx for each palette
        for (int i = 0; i < 3; i++)
        {
            // Convert the palette and set the first color to fully green like it is usually in Rayman Designer
            RGB888Color[] palette = ImageHelpers.ConvertPal666To888(levFile.MapInfo.Palettes[i]);
            palette[0] = new RGB888Color(0, 1, 0);

            // Create a pcx instance from the image data and palette
            PCX pcx = ImageHelpers.CreatePCX(scanLines, palette);

            // Save pcx file
            string tilesetFileName = i == 0 ? $"{tilesetNamePrefix}.pcx" : $"{tilesetNamePrefix}_{i}.pcx";
            string pcxFilePath = Path.Combine(mapFileDir, tilesetFileName);
            context.AddFile(new LinearFile(context, pcxFilePath));
            FileFactory.Write<PCX>(context, pcxFilePath, pcx);
        }

        Console.WriteLine("Finished exporting tileset");
    }

    private void ExportAndSaveMap(LevelFile levFile, string mapFileDir, string mapName, ushort[][] tileSetIndexMaps)
    {
        Console.WriteLine("Exporting and saving map");
        
        UniversalMap map = new UniversalMap()
        {
            Width = levFile.MapInfo.Width,
            Height = levFile.MapInfo.Height,
            Tiles = MapBlocksToTiles(levFile.MapInfo.Blocks, tileSetIndexMaps)
        };
        
        string mapFilePath = Path.Combine(mapFileDir, mapName);
        context.AddFile(new LinearFile(context, mapFilePath));
        FileFactory.Write<UniversalMap>(context, mapFilePath, map);

        Console.WriteLine("Finished exporting and saving map");
    }

    private UniversalMapBlock[] MapBlocksToTiles(Block[] blocks, ushort[][] tileSetIndexMaps)
    {
        if (tileSetIndexMaps.Length == 0)
        {
            return blocks.Select(x => new UniversalMapBlock
            {
                TileIndex = x.TileIndex,
                BlockType = x.BlockType
            }).ToArray();
        }
        else
        {
            // TODO array out of bounds..
            return blocks.Select(x => new UniversalMapBlock
            {
                TileIndex = x.RenderMode == Block.BlockRenderMode.Opaque 
                    ? tileSetIndexMaps[0][Math.Min(tileSetIndexMaps[0].Length, x.TileIndex)] 
                    : x.RenderMode == Block.BlockRenderMode.Transparent 
                        ? tileSetIndexMaps[1][Math.Min(tileSetIndexMaps[1].Length, x.TileIndex)]
                        : (ushort)0, // TODO handle fully transparent mode correctly..
                BlockType = x.BlockType
            }).ToArray();
        }
    }
}