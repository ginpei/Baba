using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Baba.Configuration;

namespace Baba.Presentation;

internal static class MascotBorderRenderer
{
    public static BitmapSource Render(
        BitmapSource source,
        string? borderColor,
        int? borderWidth)
    {
        if (borderColor is null && borderWidth is null)
        {
            return source;
        }

        var color = ParseColor(borderColor ?? BabaSettings.DefaultBorderColor);
        var width = borderWidth ?? BabaSettings.DefaultBorderWidth;
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(borderWidth),
                "Border width must be positive.");
        }

        var formattedSource = ConvertToBgra32(source);
        var sourceStride = checked(formattedSource.PixelWidth * 4);
        var sourcePixels = new byte[checked(sourceStride * formattedSource.PixelHeight)];
        formattedSource.CopyPixels(sourcePixels, sourceStride, 0);

        var outputWidth = checked(formattedSource.PixelWidth + (width * 2));
        var outputHeight = checked(formattedSource.PixelHeight + (width * 2));
        var outputStride = checked(outputWidth * 4);
        var outputPixels = new byte[checked(outputStride * outputHeight)];
        var borderMask = new bool[checked(outputWidth * outputHeight)];
        var offsets = CreateRoundedOffsets(width);

        for (var sourceY = 0; sourceY < formattedSource.PixelHeight; sourceY++)
        {
            for (var sourceX = 0; sourceX < formattedSource.PixelWidth; sourceX++)
            {
                var sourceIndex = (sourceY * sourceStride) + (sourceX * 4);
                if (sourcePixels[sourceIndex + 3] == 0)
                {
                    continue;
                }

                foreach (var offset in offsets)
                {
                    var outputX = sourceX + width + offset.X;
                    var outputY = sourceY + width + offset.Y;
                    borderMask[(outputY * outputWidth) + outputX] = true;
                }
            }
        }

        for (var maskIndex = 0; maskIndex < borderMask.Length; maskIndex++)
        {
            if (!borderMask[maskIndex])
            {
                continue;
            }

            var outputIndex = maskIndex * 4;
            outputPixels[outputIndex] = color.B;
            outputPixels[outputIndex + 1] = color.G;
            outputPixels[outputIndex + 2] = color.R;
            outputPixels[outputIndex + 3] = color.A;
        }

        for (var sourceY = 0; sourceY < formattedSource.PixelHeight; sourceY++)
        {
            for (var sourceX = 0; sourceX < formattedSource.PixelWidth; sourceX++)
            {
                var sourceIndex = (sourceY * sourceStride) + (sourceX * 4);
                if (sourcePixels[sourceIndex + 3] == 0)
                {
                    continue;
                }

                var outputIndex =
                    ((sourceY + width) * outputStride)
                    + ((sourceX + width) * 4);
                Array.Copy(sourcePixels, sourceIndex, outputPixels, outputIndex, 4);
            }
        }

        var result = BitmapSource.Create(
            outputWidth,
            outputHeight,
            source.DpiX,
            source.DpiY,
            PixelFormats.Bgra32,
            null,
            outputPixels,
            outputStride);
        result.Freeze();
        return result;
    }

    private static IReadOnlyList<(int X, int Y)> CreateRoundedOffsets(int width)
    {
        var offsets = new List<(int X, int Y)>();
        var radius = width + 0.5;
        var radiusSquared = radius * radius;

        for (var y = -width; y <= width; y++)
        {
            for (var x = -width; x <= width; x++)
            {
                if ((x * x) + (y * y) <= radiusSquared)
                {
                    offsets.Add((x, y));
                }
            }
        }

        return offsets;
    }

    private static BitmapSource ConvertToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(
            source,
            PixelFormats.Bgra32,
            null,
            0);
        converted.Freeze();
        return converted;
    }

    private static System.Windows.Media.Color ParseColor(string value)
    {
        if (value.Length != 9 || value[0] != '#')
        {
            throw new FormatException("Border color must use #AARRGGBB format.");
        }

        var color = System.Windows.Media.Color.FromArgb(
            ParseByte(value, 1),
            ParseByte(value, 3),
            ParseByte(value, 5),
            ParseByte(value, 7));
        return color;
    }

    private static byte ParseByte(string value, int startIndex) =>
        byte.Parse(
            value.AsSpan(startIndex, 2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
}
