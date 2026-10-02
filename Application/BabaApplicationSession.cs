using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Application;

internal sealed class BabaApplicationSession : IDisposable
{
    private readonly string _defaultImagePath;
    private readonly string _defaultSoundPath;
    private readonly SettingsRepository _settingsRepository;
    private IReadOnlyList<TextTailWatcher> _textWatchers;
    private bool _isStarted;

    private BabaApplicationSession(
        string dataDirectory,
        string defaultImagePath,
        string defaultSoundPath,
        BabaSettings settings,
        SettingsRepository settingsRepository,
        IReadOnlyList<TextTailWatcher> textWatchers)
    {
        DataDirectory = dataDirectory;
        _defaultImagePath = defaultImagePath;
        _defaultSoundPath = defaultSoundPath;
        Settings = settings;
        _settingsRepository = settingsRepository;
        _textWatchers = textWatchers;

        AttachWatchers(_textWatchers);
    }

    public string DataDirectory { get; }

    public BabaSettings Settings { get; private set; }

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
        var textWatchers = CreateWatchers(settings);

        return new BabaApplicationSession(
            dataDirectory,
            defaultImagePath,
            defaultSoundPath,
            settings,
            settingsRepository,
            textWatchers);
    }

    public void SaveSettings(BabaSettings settings) => _settingsRepository.Save(settings);

    public void Start()
    {
        _isStarted = true;
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

    public BabaSettings ReloadSettings()
    {
        var settings = BabaDataDirectoryInitializer.Initialize(
            _settingsRepository,
            _defaultImagePath,
            _defaultSoundPath);
        var textWatchers = CreateWatchers(settings);

        try
        {
            AttachWatchers(textWatchers);
            if (_isStarted)
            {
                foreach (var watcher in textWatchers)
                {
                    watcher.Start(readInitialLine: false);
                    watcher.ReadLatestEntry();
                }
            }
        }
        catch
        {
            DetachAndDisposeWatchers(textWatchers);
            throw;
        }

        var previousWatchers = _textWatchers;
        _textWatchers = textWatchers;
        Settings = settings;
        DetachAndDisposeWatchers(previousWatchers);
        return settings;
    }

    public void Dispose()
    {
        DetachAndDisposeWatchers(_textWatchers);
    }

    private void OnLastLineChanged(object? sender, string message) =>
        SpeechUpdated?.Invoke(this, message);

    private void OnReadFailed(object? sender, Exception exception) =>
        SpeechReadFailed?.Invoke(this, exception);

    private static IReadOnlyList<TextTailWatcher> CreateWatchers(BabaSettings settings) =>
        settings.SpeechSources
            .Select(source => new TextTailWatcher(source.SpeechFilePath))
            .ToArray();

    private void AttachWatchers(IReadOnlyList<TextTailWatcher> textWatchers)
    {
        foreach (var watcher in textWatchers)
        {
            watcher.LastLineChanged += OnLastLineChanged;
            watcher.ReadFailed += OnReadFailed;
        }
    }

    private void DetachAndDisposeWatchers(IReadOnlyList<TextTailWatcher> textWatchers)
    {
        foreach (var watcher in textWatchers)
        {
            watcher.LastLineChanged -= OnLastLineChanged;
            watcher.ReadFailed -= OnReadFailed;
            watcher.Dispose();
        }
    }
}
