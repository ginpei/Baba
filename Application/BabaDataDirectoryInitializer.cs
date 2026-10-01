using System.IO;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Application;

public static class BabaDataDirectoryInitializer
{
    private const string InitialSpeechText = """
        Welcome to Baba.

        Add a line in this format to display its message in Baba's speech bubble:
        - YYYY-MM-DD HH:mm:ss <message>

        Hold Ctrl to keep Baba visible. Drag with the left mouse button to move it, or right-click to open its menu.
        """;

    public static BabaSettings Initialize(
        SettingsRepository settingsRepository,
        string defaultImagePath,
        string defaultSoundPath)
    {
        var settings = settingsRepository.Load();
        var isNewConfig = settings is null;
        settings ??= CreateDefaultSettings(settingsRepository.DataDirectory);
        var isSoundSettingMissing = settings.MessageSoundPath is null;
        var messageSoundPath = isSoundSettingMissing
            ? ResolvePath("se-progress.wav", settingsRepository.DataDirectory)
            : ResolveOptionalPath(settings.MessageSoundPath, settingsRepository.DataDirectory);

        var resolvedSettings = new BabaSettings
        {
            MascotImagePath = ResolvePath(settings.MascotImagePath, settingsRepository.DataDirectory),
            MessageSoundPath = messageSoundPath,
            SpeechSources = settings.SpeechSources
                .Select(source => new SpeechSourceSettings
                {
                    SpeechFilePath = ResolvePath(source.SpeechFilePath, settingsRepository.DataDirectory),
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
        if (!string.IsNullOrEmpty(resolvedSettings.MessageSoundPath))
        {
            EnsureSoundFile(resolvedSettings.MessageSoundPath, defaultSoundPath);
        }

        if (isNewConfig || isSoundSettingMissing)
        {
            settingsRepository.Save(resolvedSettings);
        }

        return resolvedSettings;
    }

    private static BabaSettings CreateDefaultSettings(string dataDirectory) => new()
    {
        MascotImagePath = Path.Combine(dataDirectory, "mascot.png"),
        MessageSoundPath = Path.Combine(dataDirectory, "se-progress.wav"),
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

    private static string ResolveOptionalPath(string path, string dataDirectory) =>
        path.Length == 0 ? string.Empty : ResolvePath(path, dataDirectory);

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
        EnsureAssetFile(
            imageFilePath,
            defaultImagePath,
            "The bundled default mascot image was not found.");
    }

    private static void EnsureSoundFile(string soundFilePath, string defaultSoundPath)
    {
        EnsureAssetFile(
            soundFilePath,
            defaultSoundPath,
            "The bundled default message sound was not found.");
    }

    private static void EnsureAssetFile(
        string filePath,
        string defaultFilePath,
        string missingFileMessage)
    {
        EnsureParentDirectory(filePath);
        if (!File.Exists(filePath))
        {
            if (!File.Exists(defaultFilePath))
            {
                throw new FileNotFoundException(missingFileMessage, defaultFilePath);
            }

            File.Copy(defaultFilePath, filePath);
        }
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("The file path must include a directory.", nameof(filePath));
        Directory.CreateDirectory(directory);
    }
}
