using System.IO;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Application;

public static class BabaDataDirectoryInitializer
{
    private const string InitialSpeechText = """
        Welcome to Baba.

        Add a new non-empty line to this file to display it in Baba's speech bubble.
        Hold Ctrl to keep Baba visible. Drag with the left mouse button to move it, or right-click to open its menu.
        """;

    public static BabaSettings Initialize(SettingsRepository settingsRepository, string defaultImagePath)
    {
        var settings = settingsRepository.Load();
        var isNewConfig = settings is null;
        settings ??= CreateDefaultSettings(settingsRepository.DataDirectory);

        var resolvedSettings = new BabaSettings
        {
            MascotImagePath = ResolvePath(settings.MascotImagePath, settingsRepository.DataDirectory),
            SpeechSources = settings.SpeechSources
                .Select(source => new SpeechSourceSettings
                {
                    SpeechFilePath = ResolvePath(source.SpeechFilePath, settingsRepository.DataDirectory),
                    SpeechLinePattern = source.SpeechLinePattern,
                })
                .ToArray(),
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
            WindowLeft = settings.WindowLeft,
            WindowTop = settings.WindowTop,
        };

        foreach (var source in resolvedSettings.SpeechSources)
        {
            EnsureSpeechFile(source.SpeechFilePath, isNewConfig);
        }

        EnsureImageFile(resolvedSettings.MascotImagePath, defaultImagePath);

        if (isNewConfig)
        {
            settingsRepository.Save(resolvedSettings);
        }

        return resolvedSettings;
    }

    private static BabaSettings CreateDefaultSettings(string dataDirectory) => new()
    {
        MascotImagePath = Path.Combine(dataDirectory, "mascot.png"),
        SpeechSources =
        [
            new SpeechSourceSettings
            {
                SpeechFilePath = Path.Combine(dataDirectory, "moments.txt"),
            },
        ],
    };

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
