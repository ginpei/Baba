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
        var imageContents = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(defaultImagePath, imageContents);
        var repository = new SettingsRepository(dataDirectory);

        var settings = BabaDataDirectoryInitializer.Initialize(repository, defaultImagePath);

        Assert.Equal(System.IO.Path.Combine(dataDirectory, "mascot.png"), settings.MascotImagePath);
        Assert.Equal(System.IO.Path.Combine(dataDirectory, "moments.txt"), settings.SpeechFilePath);
        Assert.Contains("Welcome to Baba.", File.ReadAllText(settings.SpeechFilePath));
        Assert.Equal(imageContents, File.ReadAllBytes(settings.MascotImagePath));
        Assert.Equal(settings.MascotImagePath, repository.Load()?.MascotImagePath);
        Assert.Equal(settings.SpeechFilePath, repository.Load()?.SpeechFilePath);
    }

    [Fact]
    public void Initialize_ResolvesRelativePathsAndPreservesExistingFilesAndSettings()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var imagePath = System.IO.Path.Combine(dataDirectory, "images", "mascot.png");
        var speechPath = System.IO.Path.Combine(dataDirectory, "text", "moments.txt");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(imagePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(speechPath)!);
        var imageContents = new byte[] { 5, 6, 7 };
        File.WriteAllBytes(imagePath, imageContents);
        File.WriteAllText(speechPath, "Existing speech");
        repository.Save(new BabaSettings
        {
            MascotImagePath = System.IO.Path.Combine("images", "mascot.png"),
            SpeechFilePath = System.IO.Path.Combine("text", "moments.txt"),
            SpeechLinePattern = @"^say: (.+)$",
            WindowLeft = 11,
            WindowTop = 22,
            WindowWidth = 333,
            WindowHeight = 444,
        });

        var settings = BabaDataDirectoryInitializer.Initialize(
            repository,
            System.IO.Path.Combine(directory.Path, "unused-default.png"));

        Assert.Equal(System.IO.Path.GetFullPath(imagePath), settings.MascotImagePath);
        Assert.Equal(System.IO.Path.GetFullPath(speechPath), settings.SpeechFilePath);
        Assert.Equal("Existing speech", File.ReadAllText(speechPath));
        Assert.Equal(imageContents, File.ReadAllBytes(imagePath));
        Assert.Equal(@"^say: (.+)$", settings.SpeechLinePattern);
        Assert.Equal(333, settings.WindowWidth);
        Assert.Equal(444, settings.WindowHeight);
    }

    [Fact]
    public void Initialize_CopiesDefaultImageWhenConfiguredImageIsMissing()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        var repository = new SettingsRepository(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        var imageContents = new byte[] { 8, 9, 10 };
        File.WriteAllBytes(defaultImagePath, imageContents);
        repository.Save(new BabaSettings
        {
            MascotImagePath = System.IO.Path.Combine("images", "mascot.png"),
            SpeechFilePath = "moments.txt",
        });

        var settings = BabaDataDirectoryInitializer.Initialize(repository, defaultImagePath);

        Assert.Equal(imageContents, File.ReadAllBytes(settings.MascotImagePath));
        Assert.True(File.Exists(settings.SpeechFilePath));
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
            SpeechFilePath = "moments.txt",
        });
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "missing.png");

        var exception = Assert.Throws<FileNotFoundException>(
            () => BabaDataDirectoryInitializer.Initialize(repository, defaultImagePath));

        Assert.Equal(defaultImagePath, exception.FileName);
    }
}
