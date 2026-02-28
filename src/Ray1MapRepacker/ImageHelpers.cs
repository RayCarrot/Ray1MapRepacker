using BinarySerializer;
using BinarySerializer.Image;

namespace Ray1MapRepacker;

public static class ImageHelpers
{
    /// <summary>
    /// Converts a palette of RGB666 colors to a palette with RGB888 colors
    /// </summary>
    /// <param name="pal666">The palette to convert</param>
    /// <returns>The converted palette</returns>
    public static RGB888Color[] ConvertPal666To888(RGB666Color[] pal666)
    {
        RGB888Color[] pal888 = new RGB888Color[pal666.Length];
        for (int i = 0; i < pal666.Length; i++)
        {
            RGB666Color color = pal666[i];
            pal888[i] = new RGB888Color(color.Red, color.Green, color.Blue);
        }

        return pal888;
    }

    /// <summary>
    /// Converts a palette of RGB888 colors to a palette with RGB666 colors
    /// </summary>
    /// <param name="pal888">The palette to convert</param>
    /// <returns>The converted palette</returns>
    public static RGB666Color[] ConvertPal888To666(RGB888Color[] pal888)
    {
        RGB666Color[] pal666 = new RGB666Color[pal888.Length];
        for (int i = 0; i < pal888.Length; i++)
        {
            RGB888Color color = pal888[i];
            pal666[i] = new RGB666Color(color.Red, color.Green, color.Blue);
        }

        return pal666;
    }


    // Map all indices of the lev file palettes onto the PCX palette, to allow a comparison between tiles later on
    public static byte[][] CreateColorPaletteIndexMap(RGB666Color[][] palettesLevel, RGB666Color[] palettePCX)
    {
        if(palettesLevel.Length == 0 || palettePCX.Length == 0)
            return [];
        
        // Create a mapping for every palette in the level
        byte[][] indexMap = new byte[palettesLevel.Length][];
        for (ushort palIndex = 0; palIndex < palettesLevel.Length; palIndex++)
        {
            RGB666Color[] palette = palettesLevel[palIndex];
            HashSet<ushort> zeroMappings = [];
            // Handle empty palette
            if (palettePCX.Length == 0)
            {
                indexMap[palIndex] = [];
                continue;
            }
            
            // Map every palette color index onto one of the PCX palette
            indexMap[palIndex] = new byte[palette.Length];
            for (ushort colorIndex = 0; colorIndex != palette.Length; colorIndex++)
            {
                RGB666Color currentColor = palette[colorIndex];
                int index = Array.FindIndex(palettePCX, c => CompareColorsFuzzy(currentColor, c, 0f));
                byte clampedIndex = (byte) Math.Min(palette.Length - 1, Math.Max(0, index));

                if (index != -1)
                    Console.WriteLine($"mapping colorIndex {colorIndex}->{clampedIndex} with color: {currentColor}->{palettePCX[clampedIndex]}");

                if (index == -1)
                    zeroMappings.Add(colorIndex);
                
                indexMap[palIndex][colorIndex] = clampedIndex;
            }


            if (zeroMappings.Count != 0)
                Console.WriteLine($"mapped {palette.Length - zeroMappings.Count} of {palette.Length} without threshold ({zeroMappings.Count} invalid) for palette {palIndex}");
            
            const float startThreshold = 0.008f; // rounding errors are at around 0.016f
            const float baseThreshold = 0.004f; // lower value => higher accuracy - 0.004f is around value 1 difference in RGBA value
            const byte maxIterationCount = 32;  // higher value => more mapping hits, but larger color differences in mapping possible and longer processing time
            // => up to RGB value difference for each color value of 34 is possible with startThreshold = 0.008f, baseThreshold = 0.004f and maxIterationCount = 32
            for (byte thresholdIteration = 0; thresholdIteration < maxIterationCount; thresholdIteration++)
            {
                if (zeroMappings.Count == 0)
                    break;
                
                ushort[] currentZeroMappings = zeroMappings.ToArray();
                
                foreach (ushort colorIndex in currentZeroMappings)
                {
                    RGB666Color currentColor = palette[colorIndex];
                    float threshold = startThreshold + (thresholdIteration * baseThreshold);
                    
                    int index = Array.FindIndex(palettePCX, c => CompareColorsFuzzy(currentColor, c, threshold));
                    if (index != -1)
                    {
                        zeroMappings.Remove(colorIndex);
                        byte clampedIndex = (byte) Math.Min(palette.Length - 1, Math.Max(0, index));
                        Console.WriteLine($"retried mapping colorIndex {colorIndex}->{index} with color: {currentColor}->{palettePCX[index]} in iteration {thresholdIteration}");
                        indexMap[palIndex][colorIndex] = clampedIndex;
                    }
                }
            }
            
            Console.WriteLine($"mapped {palette.Length - zeroMappings.Count} of {palette.Length} ({zeroMappings.Count} invalid) for palette {palIndex}");
        }
        
        return indexMap;
    }

    private static bool CompareColorsFuzzy(RGB666Color color0, RGB666Color color1, float threshold)
    {
        return Math.Abs(color0.Red - color1.Red) <= threshold
            && Math.Abs(color0.Green - color1.Green) <= threshold
            && Math.Abs(color0.Blue - color1.Blue) <= threshold;
    }

    /// <summary>
    /// Creates a new PCX instance from image data
    /// </summary>
    /// <param name="scanLines">The image scan-lines</param>
    /// <param name="palette">The 256-color palette</param>
    /// <returns>The PCX instance</returns>
    public static PCX CreatePCX(byte[][] scanLines, RGB888Color[] palette)
    {
        // Determine the width and height from the scan-lines
        int width = scanLines[0].Length;
        int height = scanLines.Length;

        // Create an EGA palette by truncating the VGA palette (probably don't need to do this?)
        RGB888Color[] egaPalette = new RGB888Color[16];
        for (int i = 0; i < 16; i++)
            egaPalette[i] = new RGB888Color(palette[i].Red, palette[i].Green, palette[i].Blue);

        // Create and return the PCX instance
        return new PCX()
        {
            Manufacturer = 10,
            Version = 5,
            Encoding = PCX_Encoding.RLE,
            BitsPerPixel = 8,
            XStart = 0,
            YStart = 0,
            XEnd = (ushort)(width - 1),
            YEnd = (ushort)(height - 1),
            HorizontalDPI = 150,
            VerticalDPI = 150,
            EGAPalette = egaPalette,
            BitPlaneCount = 1,
            BytesPerLine = (ushort)width,
            PaletteType = 1,
            ScanLines = scanLines,
            VGAPaletteStart = 12,
            VGAPalette = palette
        };
    }
}