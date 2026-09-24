using Baba.Application;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class BabaApplicationSessionTests
{
    [Fact]
    public async Task Start_ForwardsSpeechUpdatesFromEachConfiguredSource()
    {
        using var directory = new TemporaryDirectory();
        var dataDirectory = System.IO.Path.Combine(directory.Path, "data");
        Directory.CreateDirectory(dataDirectory);
        var defaultImagePath = System.IO.Path.Combine(directory.Path, "default.png");
        File.WriteAllBytes(defaultImagePath, [1, 2, 3]);
        var firstSpeechPath = System.IO.Path.Combine(dataDirectory, "first.txt");
        var secondSpeechPath = System.IO.Path.Combine(dataDirectory, "second.txt");
        File.WriteAllText(firstSpeechPath, string.Empty);
        File.WriteAllText(secondSpeechPath, string.Empty);
        var repository = new SettingsRepository(dataDirectory);
        repository.Save(new BabaSettings
        {
            MascotImagePath = "mascot.png",
            SpeechSources =
            [
                new SpeechSourceSettings
                {
                    SpeechFilePath = "first.txt",
                },
                new SpeechSourceSettings
                {
                    SpeechFilePath = "second.txt",
                    SpeechLinePattern = @"^say: (.+)$",
                },
            ],
        });

        using var session = BabaApplicationSession.Create(dataDirectory, defaultImagePath);
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
        File.AppendAllText(firstSpeechPath, $"First source{Environment.NewLine}");
        await firstUpdate.Task.WaitAsync(TimeSpan.FromSeconds(10));
        File.AppendAllText(secondSpeechPath, $"say: Second source{Environment.NewLine}");
        await secondUpdate.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
