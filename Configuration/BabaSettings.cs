namespace Baba.Configuration;

public sealed class BabaSettings
{
    public string MascotImagePath { get; init; } = string.Empty;

    public IReadOnlyList<SpeechSourceSettings> SpeechSources { get; init; } = [];

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }

    public double? WindowLeft { get; init; }

    public double? WindowTop { get; init; }

    public BabaSettings WithWindowBounds(double left, double top, double width, double height)
    {
        if (!double.IsFinite(left)
            || !double.IsFinite(top)
            || !double.IsFinite(width)
            || width <= 0
            || !double.IsFinite(height)
            || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Window bounds must be finite, and dimensions must be positive.");
        }

        return new BabaSettings
        {
            MascotImagePath = MascotImagePath,
            SpeechSources = SpeechSources,
            WindowWidth = width,
            WindowHeight = height,
            WindowLeft = left,
            WindowTop = top,
        };
    }
}
