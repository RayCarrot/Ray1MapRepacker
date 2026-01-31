using BinarySerializer;
using BinarySerializer.Image;
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
    /// <param name="tilesetNamePrefix"> Name prefix for the created tileset file. </param>
    /// <param name="mapFileName"> Name of the map file, the lev data is exported to. </param>
    public void ExportLevel(string levFilePath, string mapFileDir, string tilesetNamePrefix, string mapFileName)
    {
        Console.WriteLine($"Starting export process for {levFilePath}");
    
        LevelFile levFile = ContextHelper.ReadLevelFile(context, levFilePath);
        ExportTileSet(levFile, mapFileDir, tilesetNamePrefix);
        ExportMap(levFile, mapFileDir, mapFileName);
    
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

    private void ExportMap(LevelFile levFile, string mapFileDir, string mapName)
    {
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

        string mapFilePath = Path.Combine(mapFileDir, mapName);
        context.AddFile(new LinearFile(context, mapFilePath));
        FileFactory.Write<UniversalMap>(context, mapFilePath, map);

        Console.WriteLine("Finished exporting map");
    }
}