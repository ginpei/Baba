using Baba.Configuration;

namespace Baba.Tests;

public sealed class BabaSettingsTests
{
    [Fact]
    public void WithWindowBounds_PreservesSettingsAndReplacesBounds()
    {
        var settings = new BabaSettings
        {
            MascotImagePath = "mascot.png",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "moments.txt",
                    SpeechLinePattern = @"^say: (.+)$",
                },
            ],
            WindowLeft = 1,
            WindowTop = 2,
            WindowWidth = 300,
            WindowHeight = 400,
        };

        var updated = settings.WithWindowBounds(-10, 20, 500, 600);

        Assert.Equal("mascot.png", updated.MascotImagePath);
        Assert.Single(updated.SpeechSources);
        Assert.Equal("moments.txt", updated.SpeechSources[0].SpeechFilePath);
        Assert.Equal(@"^say: (.+)$", updated.SpeechSources[0].SpeechLinePattern);
        Assert.Equal(-10, updated.WindowLeft);
        Assert.Equal(20, updated.WindowTop);
        Assert.Equal(500, updated.WindowWidth);
        Assert.Equal(600, updated.WindowHeight);
    }

    [Theory]
    [InlineData(double.NaN, 0, 100, 100)]
    [InlineData(double.PositiveInfinity, 0, 100, 100)]
    [InlineData(0, double.NaN, 100, 100)]
    [InlineData(0, double.NegativeInfinity, 100, 100)]
    [InlineData(0, 0, 0, 100)]
    [InlineData(0, 0, -1, 100)]
    [InlineData(0, 0, double.NaN, 100)]
    [InlineData(0, 0, double.PositiveInfinity, 100)]
    [InlineData(0, 0, 100, 0)]
    [InlineData(0, 0, 100, -1)]
    [InlineData(0, 0, 100, double.NaN)]
    [InlineData(0, 0, 100, double.PositiveInfinity)]
    public void WithWindowBounds_RejectsInvalidBounds(
        double left,
        double top,
        double width,
        double height)
    {
        var settings = new BabaSettings();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => settings.WithWindowBounds(left, top, width, height));
    }
}
