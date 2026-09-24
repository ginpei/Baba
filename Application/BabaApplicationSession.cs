using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Application;

internal sealed class BabaApplicationSession : IDisposable
{
    private readonly SettingsRepository _settingsRepository;
    private readonly IReadOnlyList<TextTailWatcher> _textWatchers;

    private BabaApplicationSession(
        string dataDirectory,
        BabaSettings settings,
        SettingsRepository settingsRepository,
        IReadOnlyList<TextTailWatcher> textWatchers)
    {
        DataDirectory = dataDirectory;
        Settings = settings;
        _settingsRepository = settingsRepository;
        _textWatchers = textWatchers;
    }

    public string DataDirectory { get; }

    public BabaSettings Settings { get; }

    public event EventHandler<Exception>? SpeechReadFailed
    {
        add
        {
            foreach (var watcher in _textWatchers)
            {
                watcher.ReadFailed += value;
            }
        }
        remove
        {
            foreach (var watcher in _textWatchers)
            {
                watcher.ReadFailed -= value;
            }
        }
    }

    public event EventHandler<string>? SpeechUpdated
    {
        add
        {
            foreach (var watcher in _textWatchers)
            {
                watcher.LastLineChanged += value;
            }
        }
        remove
        {
            foreach (var watcher in _textWatchers)
            {
                watcher.LastLineChanged -= value;
            }
        }
    }

    public static BabaApplicationSession Create(string dataDirectory, string defaultImagePath)
    {
        var settingsRepository = new SettingsRepository(dataDirectory);
        var settings = BabaDataDirectoryInitializer.Initialize(settingsRepository, defaultImagePath);
        var speechSources = settings.SpeechSources
            .Select(source => (
                source.SpeechFilePath,
                LinePattern: SpeechLineParser.CreatePattern(source.SpeechLinePattern)))
            .ToArray();
        var textWatchers = speechSources
            .Select(source => new TextTailWatcher(source.SpeechFilePath, source.LinePattern))
            .ToArray();

        return new BabaApplicationSession(dataDirectory, settings, settingsRepository, textWatchers);
    }

    public void SaveSettings(BabaSettings settings) => _settingsRepository.Save(settings);

    public void Start()
    {
        foreach (var watcher in _textWatchers)
        {
            watcher.Start();
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _textWatchers)
        {
            watcher.Dispose();
        }
    }
}
