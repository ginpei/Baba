using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Baba.Services;

public sealed class BabaSettings
{
    public string MascotImagePath { get; init; } = string.Empty;

    public string SpeechFilePath { get; init; } = string.Empty;

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }

    public double? WindowLeft { get; init; }

    public double? WindowTop { get; init; }
}

public static class SettingsService
{
    private const string ConfigFileName = "baba.json";
    private const string InitialSpeechText = """
        Welcome to Baba.

        Add a new non-empty line to this file to display it in Baba's speech bubble.
        Hold Ctrl to keep Baba visible. Drag with the left mouse button to move it, or right-click to open its menu.
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static BabaSettings LoadOrCreate(string dataDirectory, string defaultImagePath)
    {
        Directory.CreateDirectory(dataDirectory);

        var configPath = Path.Combine(dataDirectory, ConfigFileName);
        var isNewConfig = !File.Exists(configPath);
        var settings = isNewConfig
            ? CreateDefaultSettings(dataDirectory)
            : LoadSettings(configPath);

        var resolvedSettings = new BabaSettings
        {
            MascotImagePath = ResolvePath(settings.MascotImagePath, dataDirectory),
            SpeechFilePath = ResolvePath(settings.SpeechFilePath, dataDirectory),
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
            WindowLeft = settings.WindowLeft,
            WindowTop = settings.WindowTop,
        };

        EnsureSpeechFile(resolvedSettings.SpeechFilePath, isNewConfig);
        EnsureImageFile(resolvedSettings.MascotImagePath, defaultImagePath);

        if (isNewConfig)
        {
            File.WriteAllText(
                configPath,
                JsonSerializer.Serialize(resolvedSettings, SerializerOptions));
        }

        return resolvedSettings;
    }

    public static BabaSettings SaveWindowBounds(
        string dataDirectory,
        BabaSettings settings,
        double left,
        double top,
        double width,
        double height)
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

        var updatedSettings = new BabaSettings
        {
            MascotImagePath = settings.MascotImagePath,
            SpeechFilePath = settings.SpeechFilePath,
            WindowWidth = width,
            WindowHeight = height,
            WindowLeft = left,
            WindowTop = top,
        };
        File.WriteAllText(
            Path.Combine(dataDirectory, ConfigFileName),
            JsonSerializer.Serialize(updatedSettings, SerializerOptions));
        return updatedSettings;
    }

    private static BabaSettings CreateDefaultSettings(string dataDirectory) => new()
    {
        MascotImagePath = Path.Combine(dataDirectory, "mascot.png"),
        SpeechFilePath = Path.Combine(dataDirectory, "speech.txt"),
    };

    private static BabaSettings LoadSettings(string configPath)
    {
        var settings = JsonSerializer.Deserialize<BabaSettings>(File.ReadAllText(configPath))
            ?? throw new JsonException($"The configuration file '{configPath}' is empty.");

        if (string.IsNullOrWhiteSpace(settings.MascotImagePath)
            || string.IsNullOrWhiteSpace(settings.SpeechFilePath))
        {
            throw new JsonException($"The configuration file '{configPath}' requires MascotImagePath and SpeechFilePath.");
        }

        return settings;
    }

    private static string ResolvePath(string path, string dataDirectory) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(dataDirectory, path));

    private static void EnsureSpeechFile(string speechFilePath, bool writeInitialText)
    {
        EnsureParentDirectory(speechFilePath);
        if (!File.Exists(speechFilePath)
            || (writeInitialText && new FileInfo(speechFilePath).Length == 0))
        {
            File.WriteAllText(speechFilePath, InitialSpeechText);
        }
    }

    private static void EnsureImageFile(string imageFilePath, string defaultImagePath)
    {
        EnsureParentDirectory(imageFilePath);
        if (!File.Exists(imageFilePath))
        {
            if (!File.Exists(defaultImagePath))
            {
                throw new FileNotFoundException("The bundled default mascot image was not found.", defaultImagePath);
            }

            File.Copy(defaultImagePath, imageFilePath);
        }
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("The file path must include a directory.", nameof(filePath));
        Directory.CreateDirectory(directory);
    }
}
