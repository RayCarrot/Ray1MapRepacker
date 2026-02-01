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