using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Baba.Services;

public sealed class BabaSettings
{
    public string MascotImagePath { get; init; } = string.Empty;

    public string SpeechFilePath { get; init; } = string.Empty;

    public string? SpeechLinePattern { get; init; }

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
            SpeechFilePath = SpeechFilePath,
            SpeechLinePattern = SpeechLinePattern,
            WindowWidth = width,
            WindowHeight = height,
            WindowLeft = left,
            WindowTop = top,
        };
    }
}

public sealed class SettingsRepository
{
    private const string ConfigFileName = "baba.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public SettingsRepository(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }

    public BabaSettings? Load()
    {
        var configPath = Path.Combine(DataDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<BabaSettings>(File.ReadAllText(configPath))
            ?? throw new JsonException($"The configuration file '{configPath}' is empty.");
        if (string.IsNullOrWhiteSpace(settings.MascotImagePath)
            || string.IsNullOrWhiteSpace(settings.SpeechFilePath))
        {
            throw new JsonException($"The configuration file '{configPath}' requires MascotImagePath and SpeechFilePath.");
        }

        return settings;
    }

    public void Save(BabaSettings settings)
    {
        File.WriteAllText(
            Path.Combine(DataDirectory, ConfigFileName),
            JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
