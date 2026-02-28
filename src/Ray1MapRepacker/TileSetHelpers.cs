using System.Text;
using BinarySerializer;
using BinarySerializer.Image;
using BinarySerializer.Ray1.PC;

namespace Ray1MapRepacker;

public static class TileSetHelpers
{
    private const ushort NonMatchingBlockNumber = ushort.MaxValue;
    
    public const int TileSize = 16;
    public const int TileDataLength = TileSize * TileSize;

    public const int OpaqueBlockDataLength = 0x120;
    public const int TransparentBlockDataLength = 0x220;

    // Partially re-implemented from Rayman Designer
    private static void CalculateTileCountsFromImgData(byte[][] scanLines, int width, int height,
        out int opaqueBlocksCount, out int transparentBlocksCount)
    {
        opaqueBlocksCount = 0;
        transparentBlocksCount = 0;

        for (int y = 0; y < height; y += TileSize)
        {
            for (int x = 0; x < width; x += TileSize)
            {
                // Skip first tile
                if (x == 0 && y == 0)
                    continue;

                int transparentPixelsCount = GetTransparentPixelsCountInBlock(scanLines, x, y);
                
                // Opaque
                if (transparentPixelsCount == 0)
                    opaqueBlocksCount++;
                // Transparent
                else if (transparentPixelsCount < TileDataLength)
                    transparentBlocksCount++;
            }
        }
    }

    // Re-implemented from Rayman Designer
    private static int GetTransparentPixelsCountInBlock(byte[][] scanLines, int baseX, int baseY)
    {
        int transparentPixelsCount = 0;
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                if (scanLines[baseY + y][baseX + x] == 0)
                    transparentPixelsCount++;
            }
        }

        return transparentPixelsCount;
    }

    /// <summary>
    /// Converts a binary PC tile-set to PCX scan-lines
    /// </summary>
    /// <param name="levFile">The level file with the tile-set to convert</param>
    /// <returns>The tile-set scan-lines</returns>
    public static byte[][] PCBinaryTileSetToPCXScanLines(LevelFile levFile)
    {
        // Hard-code the size to 640x480 since that's what Rayman Designer uses and fits perfectly for 1200 tiles
        const int tileSetCountX = 40;
        const int tileSetCountY = 30;
        const int tileSetPixelsWidth = tileSetCountX * TileSize;
        const int tileSetPixelsHeight = tileSetCountY * TileSize;

        // Create the scan-lines
        byte[][] scanLines = new byte[tileSetPixelsHeight][];
        for (int y = 0; y < tileSetPixelsHeight; y++)
            scanLines[y] = new byte[tileSetPixelsWidth];

        // Process each tile
        foreach (TileSetBlock blockTexture in levFile.TileSetNormal.MapBlocks.OpaqueBlocks.Concat(levFile.TileSetNormal.MapBlocks.TransparentBlocks))
        {
            // Get the tile index from the offset array
            long offset = blockTexture.Offset.SerializedOffset - levFile.TileSetNormal.MapBlocks.Offset.SerializedOffset;
            int tileIndex = Array.IndexOf(levFile.TileSetNormal.BlocksOffsetTable, (uint)offset);
            
            // Determine the position in the tile-set
            int tileSetX = (tileIndex % tileSetCountX) * TileSize;
            int tileSetY = (tileIndex / tileSetCountX) * TileSize;

            // Set each pixel
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    scanLines[tileSetY + y][tileSetX + x] = (byte)(255 - blockTexture.ImgData[y * TileSize + x]);
                }
            }
        }

        return scanLines;
    }

    /// <summary>
    /// Converts PCX scan-lines to a binary PC tile-set
    /// </summary>
    /// <param name="scanLines">The tile-set scan-lines</param>
    /// <returns>The binary PC tile-set</returns>
    public static TileSetNormal PCXScanLinesToPCBinaryTileSet(byte[][] scanLines)
    {
        // Partially re-implemented from Rayman Designer

        // Determine the width and height from the scan-lines
        int tileSetPixelsWidth = scanLines[0].Length;
        int tileSetPixelsHeight = scanLines.Length;
        int tileSetWidth = tileSetPixelsWidth / TileSize;
        int tileSetHeight = tileSetPixelsHeight / TileSize;

        // Get the tile counts
        CalculateTileCountsFromImgData(scanLines, tileSetPixelsWidth, tileSetPixelsHeight, 
            out int opaqueBlocksCount, out int transparentBlocksCount);

        // Add one for the full transparent tile
        opaqueBlocksCount++;

        // Create arrays for the data
        uint[] blocksOffsetTable = new uint[1200];
        TileSetBlock[] opaqueBlocks = new TileSetBlock[opaqueBlocksCount];
        TileSetBlock[] transparentBlocks = new TileSetBlock[transparentBlocksCount];

        int blockIndex = 0;
        int opaqueBlockIndex = 0;
        int transparentBlockIndex = 0;

        // Force first tile to be transparent
        opaqueBlocks[opaqueBlockIndex] = new() { ImgData = new byte[TileDataLength], TransparencyMode = 0xAAAAAAAA };
        Array.Fill(opaqueBlocks[opaqueBlockIndex].ImgData, (byte)0xFF);
        blocksOffsetTable[blockIndex] = 0;
        opaqueBlockIndex++;
        blockIndex++;

        // Enumerate every tile
        for (int tileY = 0; tileY < tileSetHeight; tileY++)
        {
            for (int tileX = 0; tileX < tileSetWidth; tileX++)
            {
                // Skip first tile
                if (tileX == 0 && tileY == 0)
                    continue;

                // Get the amount of transparent pixels in the tile
                int transparentPixelsCount = GetTransparentPixelsCountInBlock(scanLines, tileX * TileSize, tileY * TileSize);

                // Create a block if not fully transparent
                if (transparentPixelsCount != TileDataLength)
                {
                    // Create a new tile block
                    TileSetBlock block = new() { ImgData = new byte[TileDataLength], };
                    // Opaque
                    if (transparentPixelsCount == 0)
                    {
                        opaqueBlocks[opaqueBlockIndex] = block;
                        blocksOffsetTable[blockIndex] = (uint)(opaqueBlockIndex * OpaqueBlockDataLength);
                        opaqueBlockIndex++;
                    }
                    // Transparent
                    else
                    {
                        block.Alpha = new byte[TileDataLength];
                        transparentBlocks[transparentBlockIndex] = block;
                        blocksOffsetTable[blockIndex] = (uint)(opaqueBlocksCount * OpaqueBlockDataLength + transparentBlockIndex * TransparentBlockDataLength);
                        transparentBlockIndex++;
                    }

                    // Copy pixels and set transparency mode
                    uint transparencyMode = 0;
                    int imgDataIndex = 0;
                    for (int pixelY = 0; pixelY < TileSize; pixelY++)
                    {
                        byte rowTransparentPixelsCount = 0;

                        for (int pixelX = 0; pixelX < TileSize; pixelX++)
                        {
                            // Get the pixel and copy it
                            byte pixel = scanLines[tileY * TileSize + pixelY][tileX * TileSize + pixelX];
                            block.ImgData[imgDataIndex] = (byte)(255 - pixel);

                            // Set alpha for transparent tiles
                            if (transparentPixelsCount != 0)
                            {
                                if (pixel == 0)
                                    block.Alpha[imgDataIndex] = 0;
                                else
                                    block.Alpha[imgDataIndex] = 0xFF;
                            }

                            if (pixel == 0)
                                rowTransparentPixelsCount++;

                            imgDataIndex++;
                        }

                        // Set the two bits for this row
                        if (rowTransparentPixelsCount == 0)
                            transparencyMode += 1;
                        else if (rowTransparentPixelsCount == TileSize)
                            transparencyMode += 2;
                        else
                            transparencyMode += 3;

                        // Shift for the next value
                        if (pixelY < TileSize - 1)
                            transparencyMode <<= 2;
                    }

                    // 2 bits per row
                    block.TransparencyMode = transparencyMode;
                }

                blockIndex++;
            }
        }

        return new TileSetNormal()
        {
            BlocksOffsetTable = blocksOffsetTable,
            TotalBlocksCount = (uint)(opaqueBlocks.Length + transparentBlocks.Length),
            OpaqueBlocksCount = (uint)opaqueBlocks.Length,
            MapBlocksSize = (uint)(opaqueBlocksCount * OpaqueBlockDataLength + transparentBlocksCount * TransparentBlockDataLength + 32),
            MapBlocks = new TileSetNormalMapBlocks
            {
                OpaqueBlocks = opaqueBlocks,
                TransparentBlocks = transparentBlocks,
                UnknownBytes = new byte[32]
            }
        };
    }

    public static ushort[][] CreateTileSetIndexMaps(LevelFile levelFile, PCX pcx)
    {
        // Get PCX palette
        RGB666Color[] palettePCX = ImageHelpers.ConvertPal888To666(pcx.VGAPalette);
        palettePCX[0] = new RGB666Color(0, 0, 0);

        // Create palette mapping
        byte[][] colorIndexMap = ImageHelpers.CreateColorPaletteIndexMap(levelFile.MapInfo.Palettes, palettePCX);
        
        TileSetNormalMapBlocks targetBlocks = PCXScanLinesToPCBinaryTileSet(pcx.ScanLines).MapBlocks;

        ushort[] opaqueMap =
            CreateTileSetIndexMap(colorIndexMap, levelFile.TileSetNormal.MapBlocks.OpaqueBlocks, targetBlocks.OpaqueBlocks);
        ushort[] transparentMap =
            CreateTileSetIndexMap(colorIndexMap, levelFile.TileSetNormal.MapBlocks.TransparentBlocks, targetBlocks.TransparentBlocks);

        return new ushort[][]
        {
            opaqueMap,
            transparentMap
        };
    }

    private static ushort[] CreateTileSetIndexMap(byte[][] colorIndexMap, TileSetBlock[] sourceBlocks, TileSetBlock[] targetBlocks)
    {
        TileSetBlock[][] alteredSourceBlocks = CreateAlteredTileSetBlocksByPaletteIndexMap(colorIndexMap, sourceBlocks);
        
        ushort[] indexMapping = [];
        for (ushort paletteIndex = 0; paletteIndex < alteredSourceBlocks.Length; paletteIndex++)
        {
            indexMapping = CreateTileSetIndexMap(alteredSourceBlocks[paletteIndex], targetBlocks, indexMapping);
        }

        return CleanNonMatchingBlockIndices(indexMapping);
    }
        
    private static TileSetBlock[][] CreateAlteredTileSetBlocksByPaletteIndexMap(byte[][] paletteIndexMap, TileSetBlock[] sourceBlocks)
    {
        if (paletteIndexMap.Length == 0 || sourceBlocks.Length == 0)
            return [];
        
        
        TileSetBlock[][] alteredBlocks = new TileSetBlock[paletteIndexMap.Length][];

        for (ushort paletteIndex = 0; paletteIndex < paletteIndexMap.Length; paletteIndex++)
        {
            alteredBlocks[paletteIndex] = CreateAlteredTileSetBlocksByPaletteIndexMap(paletteIndexMap[paletteIndex], sourceBlocks);
        }
        
        return alteredBlocks;
    }
    
    private static TileSetBlock[] CreateAlteredTileSetBlocksByPaletteIndexMap(byte[] paletteIndexMap, TileSetBlock[] sourceBlocks)
    {
        if (paletteIndexMap.Length == 0)
            return [];
        
        TileSetBlock[] alteredBlocks = new TileSetBlock[sourceBlocks.Length];

        for (ushort blockIndex = 0; blockIndex < sourceBlocks.Length; blockIndex++)
        {
            TileSetBlock sourceBlock = sourceBlocks[blockIndex];
            
            TileSetBlock alteredBlock = new TileSetBlock
            {
                ImgData = new byte[sourceBlock.ImgData.Length],
                Pre_HasAlpha = sourceBlock.Pre_HasAlpha,
                TransparencyMode = sourceBlock.TransparencyMode,
                UnkownBytes = (byte[]) sourceBlock.UnkownBytes.Clone(),
                Alpha = (byte[])sourceBlock.Alpha?.Clone()!
            };

            for (ushort pixelIndex = 0; pixelIndex < sourceBlock.ImgData.Length; pixelIndex++)
            {
                alteredBlock.ImgData[pixelIndex] = paletteIndexMap[sourceBlock.ImgData[pixelIndex]];
            }
            alteredBlocks[blockIndex] = alteredBlock;
        }
        
        return alteredBlocks;
    }

    private static ushort[] CreateTileSetIndexMap(TileSetBlock[] alteredSourceBlocks,
        TileSetBlock[] targetBlocks, ushort[] indexMapping)
    {
        if (alteredSourceBlocks.Length == 0 || targetBlocks.Length == 0)
            return indexMapping;
        
        int sourceLength = alteredSourceBlocks.Length;
        int targetLength = targetBlocks.Length;

        if (indexMapping.Length == 0)
        {
            indexMapping = new ushort[sourceLength];
            Array.Fill<ushort>(indexMapping, NonMatchingBlockNumber);
        }
        
        for (ushort sourceIndex = 0; sourceIndex < sourceLength; sourceIndex++)
        {
            if (indexMapping[sourceIndex] != NonMatchingBlockNumber)
            {
                continue;
            }
            
            byte[] sourceBlockImgData = alteredSourceBlocks[sourceIndex].ImgData;
            for (ushort targetIndex = 0; targetIndex < targetLength; targetIndex++)
            { // TODO seems to still have way to less hits. Maybe the colors require adjustment, or here is something wrong
                byte[] targetBlockImgData = targetBlocks[targetIndex].ImgData;
                if (sourceBlockImgData.SequenceEqual(targetBlockImgData))
                {
                    indexMapping[sourceIndex] = targetIndex;
                    
                    break;
                }
            }
        }
        
        return indexMapping;
    }

    private static ushort[] CleanNonMatchingBlockIndices(ushort[] indexMapping)
    {
        StringBuilder stringBuilder = new StringBuilder();
        int sourceLength = indexMapping.Length;
        int count = 0;
        
        for (ushort sourceIndex = 0; sourceIndex < sourceLength; sourceIndex++)
        {
            if (indexMapping[sourceIndex] == NonMatchingBlockNumber)
            {
                count++;
                stringBuilder.Append(sourceIndex).Append(", ");

                indexMapping[sourceIndex] = 0;
            }
        }
        stringBuilder.Remove(stringBuilder.Length - 2, 2);

        if (count > 0)
            ConsoleHelpers.WriteWarning($"No tileset match found for {count} opaque block indices: {stringBuilder}!");
        
        return indexMapping;
    }
}