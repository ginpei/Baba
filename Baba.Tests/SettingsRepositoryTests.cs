using System.Text.Json;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class SettingsRepositoryTests
{
    [Fact]
    public void Load_ReturnsNullWhenConfigurationDoesNotExist()
    {
        using var directory = new TemporaryDirectory();
        var repository = new SettingsRepository(directory.Path);

        Assert.Null(repository.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllSettings()
    {
        using var directory = new TemporaryDirectory();
        var repository = new SettingsRepository(directory.Path);
        var settings = new BabaSettings
        {
            MascotImagePath = "mascot.png",
            SpeechFilePath = "moments.txt",
            SpeechLinePattern = @"^say: (.+)$",
            WindowLeft = -12.5,
            WindowTop = 34.5,
            WindowWidth = 300,
            WindowHeight = 400,
        };

        repository.Save(settings);

        var loaded = repository.Load();
        Assert.NotNull(loaded);
        Assert.Equal(settings.MascotImagePath, loaded.MascotImagePath);
        Assert.Equal(settings.SpeechFilePath, loaded.SpeechFilePath);
        Assert.Equal(settings.SpeechLinePattern, loaded.SpeechLinePattern);
        Assert.Equal(settings.WindowLeft, loaded.WindowLeft);
        Assert.Equal(settings.WindowTop, loaded.WindowTop);
        Assert.Equal(settings.WindowWidth, loaded.WindowWidth);
        Assert.Equal(settings.WindowHeight, loaded.WindowHeight);
    }

    [Fact]
    public void Save_OmitsNullOptionalProperties()
    {
        using var directory = new TemporaryDirectory();
        var repository = new SettingsRepository(directory.Path);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            SpeechFilePath = "moments.txt",
        });

        using var json = JsonDocument.Parse(
            File.ReadAllText(System.IO.Path.Combine(directory.Path, "baba.json")));

        Assert.False(json.RootElement.TryGetProperty(nameof(BabaSettings.SpeechLinePattern), out _));
        Assert.False(json.RootElement.TryGetProperty(nameof(BabaSettings.WindowLeft), out _));
        Assert.False(json.RootElement.TryGetProperty(nameof(BabaSettings.WindowTop), out _));
        Assert.False(json.RootElement.TryGetProperty(nameof(BabaSettings.WindowWidth), out _));
        Assert.False(json.RootElement.TryGetProperty(nameof(BabaSettings.WindowHeight), out _));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("""{"MascotImagePath":"mascot.png"}""")]
    [InlineData("""{"MascotImagePath":" ","SpeechFilePath":"moments.txt"}""")]
    [InlineData("""{"MascotImagePath":"mascot.png","SpeechFilePath":" "}""")]
    public void Load_RejectsInvalidConfiguration(string json)
    {
        using var directory = new TemporaryDirectory();
        var repository = new SettingsRepository(directory.Path);
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "baba.json"),
            json);

        Assert.Throws<JsonException>(() => repository.Load());
    }
}
