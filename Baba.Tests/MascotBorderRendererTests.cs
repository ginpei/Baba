using System.Windows.Media;
using System.Windows.Media.Imaging;
using Baba.Presentation;

namespace Baba.Tests;

public sealed class MascotBorderRendererTests
{
    [Fact]
    public void Render_ReturnsSourceWhenBothSettingsAreMissing()
    {
        var source = CreateSinglePixelSource(0xFF, 0x22, 0x44, 0x66);

        var result = MascotBorderRenderer.Render(source, null, null);

        Assert.Same(source, result);
    }

    [Fact]
    public void Render_UsesDefaultWidthWhenOnlyColorIsSet()
    {
        var source = CreateSinglePixelSource(0xFF, 0x22, 0x44, 0x66);

        var result = MascotBorderRenderer.Render(source, "#80402010", null);

        Assert.Equal(9, result.PixelWidth);
        Assert.Equal(9, result.PixelHeight);
        Assert.Equal([0x10, 0x20, 0x40, 0x80], ReadPixel(result, 4, 0));
        Assert.Equal([0x66, 0x44, 0x22, 0xFF], ReadPixel(result, 4, 4));
    }

    [Fact]
    public void Render_UsesOpaqueWhiteWhenOnlyWidthIsSet()
    {
        var source = CreateSinglePixelSource(0xFF, 0x22, 0x44, 0x66);

        var result = MascotBorderRenderer.Render(source, null, 1);

        Assert.Equal(3, result.PixelWidth);
        Assert.Equal(3, result.PixelHeight);
        Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF], ReadPixel(result, 1, 0));
    }

    private static BitmapSource CreateSinglePixelSource(
        byte alpha,
        byte red,
        byte green,
        byte blue)
    {
        var source = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { blue, green, red, alpha },
            4);
        source.Freeze();
        return source;
    }

    private static byte[] ReadPixel(BitmapSource source, int x, int y)
    {
        var pixel = new byte[4];
        source.CopyPixels(
            new System.Windows.Int32Rect(x, y, 1, 1),
            pixel,
            4,
            0);
        return pixel;
    }
}
