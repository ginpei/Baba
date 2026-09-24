using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Baba.Configuration;

namespace Baba.Infrastructure;

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
            || settings.SpeechSources is null
            || settings.SpeechSources.Count == 0)
        {
            throw new JsonException(
                $"The configuration file '{configPath}' requires MascotImagePath and at least one SpeechSources entry.");
        }

        foreach (var source in settings.SpeechSources)
        {
            if (source is null || string.IsNullOrWhiteSpace(source.SpeechFilePath))
            {
                throw new JsonException(
                    $"Every SpeechSources entry in '{configPath}' requires SpeechFilePath.");
            }
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
