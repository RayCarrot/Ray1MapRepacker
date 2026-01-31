using BinarySerializer;
using BinarySerializer.Image;
using BinarySerializer.Ray1;
using BinarySerializer.Ray1.PC;

namespace Ray1MapRepacker;

public class Importer(Context context)
{
    /// <summary>
    /// Imports the tileset and map to the level from the input path.
    /// Only the first tileset PCX will be used for the tiles.
    /// The rest will only be used to import alternative palettes.
    /// </summary>
    /// <param name="levFilePath"> Path to the lev file that should be replaced by the import. </param>
    /// <param name="mapFileDir"> Path to the map dir, the lev data should be imported from. </param>
    /// <param name="tilesetNamePrefix"> Name prefix for the tileset PCX file, to import from. </param>
    /// <param name="mapFileName"> Name of the map file, the lev data should be imported from. </param>
    public void ImportLevel(string levFilePath, string mapFileDir, string tilesetNamePrefix, string mapFileName)
    {
        Console.WriteLine($"Starting import process for {levFilePath}");

        LevelFile levFile = ContextHelper.ReadLevelFile(context, levFilePath);
        ImportTileSet(levFile, mapFileDir, tilesetNamePrefix);
        ImportMap(levFile, mapFileDir, mapFileName);
        UpdateMapTileRenderModes(levFile);
        SaveFile(levFilePath, levFile);
    
        Console.WriteLine($"Finished import process for  {levFilePath}");
    }

    private void ImportTileSet(LevelFile levFile, string mapFileDir, string tilesetNamePrefix)
    {
        Console.WriteLine("Importing tileset");

        // Import PCX for each palette
        for (int i = 0; i < 3; i++)
        {
            string tileSetFileName = i == 0 ? $"{tilesetNamePrefix}.pcx" : $"{tilesetNamePrefix}_{i}.pcx";
            string pcxFilePath = Path.Combine(mapFileDir, tileSetFileName);

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
    }

    private void ImportMap(LevelFile levFile, string mapFileDir, string mapName)
    {
        Console.WriteLine("Importing map");

        string mapFilePath = Path.Combine(mapFileDir, mapName);
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
    }

    private void UpdateMapTileRenderModes(LevelFile levFile)
    {
        Console.WriteLine("Updating map tile render modes");
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
        Console.WriteLine("Finished updating map tile render modes");
    }

    private void SaveFile(string levFilePath, LevelFile levFile)
    {
        Console.WriteLine("Saving level file");
        FileFactory.Write<LevelFile>(context, levFilePath, levFile);
        Console.WriteLine("Level file saved");
    }
}