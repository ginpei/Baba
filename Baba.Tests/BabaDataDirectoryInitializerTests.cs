using Baba.Application;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class BabaDataDirectoryInitializerTests
{
    [Fact]
    public void Initialize_CreatesAndPersistsDefaultSettingsAndFiles()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "default.wav");
        var imageContents = new byte[] { 1, 2, 3, 4 };
        var soundContents = new byte[] { 5, 6, 7, 8 };
        File.WriteAllBytes(defaultImagePath, imageContents);
        File.WriteAllBytes(defaultSoundPath, soundContents);
        var repository = new SettingsRepository(dataDirectory);

        var settings = BabaDataDirectoryInitializer.Initialize(
            repository,
            defaultImagePath,
            defaultSoundPath);

        Assert.Equal(System.IO.Path.Combine(dataDirectory, "mascot.png"), settings.MascotImagePath);
        Assert.Equal(System.IO.Path.Combine(dataDirectory, "se-progress.wav"), settings.MessageSoundPath);
        var source = Assert.Single(settings.SpeechSources);
        Assert.Equal(System.IO.Path.Combine(dataDirectory, "moments.txt"), source.SpeechFilePath);
        Assert.Contains("Welcome to Baba.", File.ReadAllText(source.SpeechFilePath));
        Assert.Equal(imageContents, File.ReadAllBytes(settings.MascotImagePath));
        Assert.Equal(soundContents, File.ReadAllBytes(settings.MessageSoundPath));
        Assert.Equal(settings.MascotImagePath, repository.Load()?.MascotImagePath);
        Assert.Equal(settings.MessageSoundPath, repository.Load()?.MessageSoundPath);
        Assert.Equal(source.SpeechFilePath, repository.Load()?.SpeechSources[0].SpeechFilePath);
    }

    [Fact]
    public void Initialize_ResolvesRelativePathsAndPreservesExistingFilesAndSettings()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var imagePath = System.IO.Path.Combine(dataDirectory, "images", "mascot.png");
        var soundPath = System.IO.Path.Combine(dataDirectory, "sounds", "message.wav");
        var speechPath = System.IO.Path.Combine(dataDirectory, "text", "moments.txt");
        var filteredSpeechPath = System.IO.Path.Combine(dataDirectory, "text", "filtered.log");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(imagePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(soundPath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(speechPath)!);
        var imageContents = new byte[] { 5, 6, 7 };
        var soundContents = new byte[] { 8, 9, 10 };
        File.WriteAllBytes(imagePath, imageContents);
        File.WriteAllBytes(soundPath, soundContents);
        File.WriteAllText(speechPath, "Existing speech");
        repository.Save(new BabaSettings
        {
            BorderColor = "#80402010",
            BorderWidth = 6,
            MascotImagePath = System.IO.Path.Combine("images", "mascot.png"),
            MessageSoundPath = System.IO.Path.Combine("sounds", "message.wav"),
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = System.IO.Path.Combine("text", "moments.txt"),
                },
                new SpeechSourceSettings
                {
                    SpeechFilePath = System.IO.Path.Combine("text", "filtered.log"),
                },
            ],
            WindowLeft = 11,
            WindowTop = 22,
            WindowWidth = 333,
            WindowHeight = 444,
        });

        var settings = BabaDataDirectoryInitializer.Initialize(
            repository,
            System.IO.Path.Combine(directory.Path, "unused-default.png"),
            System.IO.Path.Combine(directory.Path, "unused-default.wav"));

        Assert.Equal("#80402010", settings.BorderColor);
        Assert.Equal(6, settings.BorderWidth);
        Assert.Equal(System.IO.Path.GetFullPath(imagePath), settings.MascotImagePath);
        Assert.Equal(System.IO.Path.GetFullPath(soundPath), settings.MessageSoundPath);
        Assert.Equal(2, settings.SpeechSources.Count);
        Assert.Equal(System.IO.Path.GetFullPath(speechPath), settings.SpeechSources[0].SpeechFilePath);
        Assert.Equal(
            System.IO.Path.GetFullPath(filteredSpeechPath),
            settings.SpeechSources[1].SpeechFilePath);
        Assert.Equal("Existing speech", File.ReadAllText(speechPath));
        Assert.Equal(imageContents, File.ReadAllBytes(imagePath));
        Assert.Equal(soundContents, File.ReadAllBytes(soundPath));
        Assert.True(File.Exists(filteredSpeechPath));
        Assert.Equal(333, settings.WindowWidth);
        Assert.Equal(444, settings.WindowHeight);
    }

    [Fact]
    public void Initialize_CopiesDefaultAssetsWhenConfiguredAssetsAreMissing()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "default.wav");
        var imageContents = new byte[] { 8, 9, 10 };
        var soundContents = new byte[] { 11, 12, 13 };
        File.WriteAllBytes(defaultImagePath, imageContents);
        File.WriteAllBytes(defaultSoundPath, soundContents);
        repository.Save(new BabaSettings
        {
            MascotImagePath = System.IO.Path.Combine("images", "mascot.png"),
            MessageSoundPath = System.IO.Path.Combine("sounds", "message.wav"),
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "moments.txt",
                },
            ],
        });

        var settings = BabaDataDirectoryInitializer.Initialize(
            repository,
            defaultImagePath,
            defaultSoundPath);

        Assert.Equal(imageContents, File.ReadAllBytes(settings.MascotImagePath));
        Assert.Equal(soundContents, File.ReadAllBytes(settings.MessageSoundPath));
        Assert.True(File.Exists(settings.SpeechSources[0].SpeechFilePath));
    }

    [Fact]
    public void Initialize_AddsDefaultSoundToExistingConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "default.wav");
        File.WriteAllBytes(defaultImagePath, [1]);
        File.WriteAllBytes(defaultSoundPath, [2, 3]);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "moments.txt",
                },
            ],
        });

        var settings = BabaDataDirectoryInitializer.Initialize(
            repository,
            defaultImagePath,
            defaultSoundPath);

        var expectedSoundPath = System.IO.Path.Combine(dataDirectory, "se-progress.wav");
        Assert.Equal(expectedSoundPath, settings.MessageSoundPath);
        Assert.Equal([2, 3], File.ReadAllBytes(expectedSoundPath));
        Assert.Equal(expectedSoundPath, repository.Load()?.MessageSoundPath);
    }

    [Fact]
    public void Initialize_PreservesEmptySoundPathWithoutCopyingDefaultSound()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        File.WriteAllBytes(defaultImagePath, [1]);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            MessageSoundPath = string.Empty,
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "moments.txt",
                },
            ],
        });

        var settings = BabaDataDirectoryInitializer.Initialize(
            repository,
            defaultImagePath,
            System.IO.Path.Combine(directory.Path, "missing.wav"));

        Assert.Equal(string.Empty, settings.MessageSoundPath);
        Assert.False(File.Exists(System.IO.Path.Combine(dataDirectory, "se-progress.wav")));
        Assert.Equal(string.Empty, repository.Load()?.MessageSoundPath);
    }

    [Fact]
    public void Initialize_ThrowsWhenDefaultImageIsMissing()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "moments.txt",
                },
            ],
        });
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "missing.png");
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "default.wav");
        File.WriteAllBytes(defaultSoundPath, [1]);

        var exception = Assert.Throws<FileNotFoundException>(
            () => BabaDataDirectoryInitializer.Initialize(
                repository,
                defaultImagePath,
                defaultSoundPath));

        Assert.Equal(defaultImagePath, exception.FileName);
    }

    [Fact]
    public void Initialize_ThrowsWhenDefaultSoundIsMissing()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        File.WriteAllBytes(defaultImagePath, [1]);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            MessageSoundPath = "message.wav",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "moments.txt",
                },
            ],
        });
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "missing.wav");

        var exception = Assert.Throws<FileNotFoundException>(
            () => BabaDataDirectoryInitializer.Initialize(
                repository,
                defaultImagePath,
                defaultSoundPath));

        Assert.Equal(defaultSoundPath, exception.FileName);
    }
}
