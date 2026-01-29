using BinarySerializer;
using BinarySerializer.Image;
using BinarySerializer.Ray1;
using BinarySerializer.Ray1.PC;

namespace Ray1MapRepacker;

public class Importer(Context context)
{
    public void ImportTileSet(LevelFile levFile, string exportDir, string tilesetNamePrefix)
    {
        Console.WriteLine("Importing tileset");

        // Import PCX for each palette
        for (int i = 0; i < 3; i++)
        {
            string tileSetFileName = i == 0 ? $"{tilesetNamePrefix}.pcx" : $"{tilesetNamePrefix}_{i}.pcx";
            string pcxFilePath = Path.Combine(exportDir, tileSetFileName);

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

    public void ImportMap(LevelFile levFile, string exportDir, string mapName)
    {
        Console.WriteLine("Importing map");

        string mapFilePath = Path.Combine(exportDir, mapName);
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

    public void UpdateMapTileRenderModes(LevelFile levFile)
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

    public void SaveFile(string levFilePath, LevelFile levFile)
    {
        Console.WriteLine("Saving level file");
        FileFactory.Write<LevelFile>(context, levFilePath, levFile);
        Console.WriteLine("Level file saved");
    }
}