using Baba.Application;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class BabaApplicationSessionTests
{
    [Fact]
    public void Start_ReportsNewestDatedEntryAcrossConfiguredSources()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        Directory.CreateDirectory(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "default.wav");
        File.WriteAllBytes(defaultImagePath, [1, 2, 3]);
        File.WriteAllBytes(defaultSoundPath, [4, 5, 6]);
        File.WriteAllLines(
            System.IO.Path.Combine(dataDirectory, "first.txt"),
            [
                "- 2026-09-30 10:00:02 Newest source",
                "- 2026-09-30 10:00:00 Later line with older date",
            ]);
        File.WriteAllText(
            System.IO.Path.Combine(dataDirectory, "second.txt"),
            "- 2026-09-30 10:00:01 Other source");
        var repository = new SettingsRepository(dataDirectory);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            MessageSoundPath = "message.wav",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "first.txt",
                },
                new SpeechSourceSettings
                {
                    SpeechFilePath = "second.txt",
                },
            ],
        });

        using var session = BabaApplicationSession.Create(
            dataDirectory,
            defaultImagePath,
            defaultSoundPath);
        var updates = new List<string>();
        session.SpeechUpdated += (_, message) => updates.Add(message);

        session.Start();

        Assert.Equal(["Newest source"], updates);
    }

    [Fact]
    public async Task Start_ForwardsSpeechUpdatesFromEachConfiguredSource()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        Directory.CreateDirectory(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        var defaultSoundPath = System.IO.Path.Combine(directory.Path, "default.wav");
        File.WriteAllBytes(defaultImagePath, [1, 2, 3]);
        File.WriteAllBytes(defaultSoundPath, [4, 5, 6]);
        var firstSpeechPath = System.IO.Path.Combine(dataDirectory, "first.txt");
        var secondSpeechPath = System.IO.Path.Combine(dataDirectory, "second.txt");
        File.WriteAllText(firstSpeechPath, string.Empty);
        File.WriteAllText(secondSpeechPath, string.Empty);
        var repository = new SettingsRepository(dataDirectory);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            MessageSoundPath = "message.wav",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "first.txt",
                },
                new SpeechSourceSettings
                {
                    SpeechFilePath = "second.txt",
                },
            ],
        });

        using var session = BabaApplicationSession.Create(
            dataDirectory,
            defaultImagePath,
            defaultSoundPath);
        var firstUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SpeechUpdated += (_, message) =>
        {
            if (message == "First source")
            {
                firstUpdate.TrySetResult();
            }
            else if (message == "Second source")
            {
                secondUpdate.TrySetResult();
            }
        };

        session.Start();
        File.AppendAllText(firstSpeechPath, $"- 2026-09-30 10:00:00 First source{Environment.NewLine}");
        await firstUpdate.Task.WaitAsync(TimeSpan.FromSeconds(10));
        File.AppendAllText(secondSpeechPath, $"- 2026-09-30 10:00:01 Second source{Environment.NewLine}");
        await secondUpdate.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
