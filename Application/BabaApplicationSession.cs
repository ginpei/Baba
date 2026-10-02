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

        foreach (var watcher in _textWatchers)
        {
            watcher.LastLineChanged += OnLastLineChanged;
            watcher.ReadFailed += OnReadFailed;
        }
    }

    public string DataDirectory { get; }

    public BabaSettings Settings { get; }

    public event EventHandler<Exception>? SpeechReadFailed;

    public event EventHandler<string>? SpeechUpdated;

    public static BabaApplicationSession Create(
        string dataDirectory,
        string defaultImagePath,
        string defaultSoundPath)
    {
        var settingsRepository = new SettingsRepository(dataDirectory);
        var settings = BabaDataDirectoryInitializer.Initialize(
            settingsRepository,
            defaultImagePath,
            defaultSoundPath);
        var textWatchers = settings.SpeechSources
            .Select(source => new TextTailWatcher(source.SpeechFilePath))
            .ToArray();

        return new BabaApplicationSession(dataDirectory, settings, settingsRepository, textWatchers);
    }

    public void SaveSettings(BabaSettings settings) => _settingsRepository.Save(settings);

    public void Start()
    {
        foreach (var watcher in _textWatchers)
        {
            watcher.Start(readInitialLine: false);
        }

        var latestEntry = _textWatchers
            .Select(watcher => watcher.ReadLatestEntry())
            .OfType<SpeechEntry>()
            .MaxBy(v => v.Timestamp);
        if (latestEntry is not null)
        {
            SpeechUpdated?.Invoke(this, latestEntry.Message);
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _textWatchers)
        {
            watcher.LastLineChanged -= OnLastLineChanged;
            watcher.ReadFailed -= OnReadFailed;
            watcher.Dispose();
        }
    }

    private void OnLastLineChanged(object? sender, string message) =>
        SpeechUpdated?.Invoke(this, message);

    private void OnReadFailed(object? sender, Exception exception) =>
        SpeechReadFailed?.Invoke(this, exception);
}
