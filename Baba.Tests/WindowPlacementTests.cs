using System.Drawing;
using Baba.Presentation;

namespace Baba.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public void IsVisibleInAnyWorkingArea_ReturnsTrue_WhenWindowOverlapsWorkingArea()
    {
        var windowBounds = new Rectangle(1800, 900, 320, 390);
        var workingAreas = new[]
        {
            new Rectangle(0, 0, 1920, 1040),
        };

        var result = WindowPlacement.IsVisibleInAnyWorkingArea(windowBounds, workingAreas);

        Assert.True(result);
    }

    [Fact]
    public void IsVisibleInAnyWorkingArea_ReturnsTrue_WhenWindowIsOnSecondaryScreen()
    {
        var windowBounds = new Rectangle(-1200, 300, 320, 390);
        var workingAreas = new[]
        {
            new Rectangle(0, 0, 1920, 1040),
            new Rectangle(-1280, 0, 1280, 984),
        };

        var result = WindowPlacement.IsVisibleInAnyWorkingArea(windowBounds, workingAreas);

        Assert.True(result);
    }

    [Fact]
    public void IsVisibleInAnyWorkingArea_ReturnsFalse_WhenRememberedScreenIsDisconnected()
    {
        var windowBounds = new Rectangle(2100, 300, 320, 390);
        var workingAreas = new[]
        {
            new Rectangle(0, 0, 1920, 1040),
        };

        var result = WindowPlacement.IsVisibleInAnyWorkingArea(windowBounds, workingAreas);

        Assert.False(result);
    }
}
