using System.Text.RegularExpressions;
using Baba.Configuration;
using Baba.Infrastructure;

namespace Baba.Application;

internal sealed class BabaApplicationSession : IDisposable
{
    private readonly SettingsRepository _settingsRepository;
    private readonly TextTailWatcher _textWatcher;

    private BabaApplicationSession(
        string dataDirectory,
        BabaSettings settings,
        SettingsRepository settingsRepository,
        TextTailWatcher textWatcher)
    {
        DataDirectory = dataDirectory;
        Settings = settings;
        _settingsRepository = settingsRepository;
        _textWatcher = textWatcher;
    }

    public string DataDirectory { get; }

    public BabaSettings Settings { get; }

    public event EventHandler<Exception>? SpeechReadFailed
    {
        add => _textWatcher.ReadFailed += value;
        remove => _textWatcher.ReadFailed -= value;
    }

    public event EventHandler<string>? SpeechUpdated
    {
        add => _textWatcher.LastLineChanged += value;
        remove => _textWatcher.LastLineChanged -= value;
    }

    public static BabaApplicationSession Create(string dataDirectory, string defaultImagePath)
    {
        var settingsRepository = new SettingsRepository(dataDirectory);
        var settings = BabaDataDirectoryInitializer.Initialize(settingsRepository, defaultImagePath);
        var textWatcher = new TextTailWatcher(
            settings.SpeechFilePath,
            CreateSpeechLinePattern(settings.SpeechLinePattern));

        return new BabaApplicationSession(dataDirectory, settings, settingsRepository, textWatcher);
    }

    public void SaveSettings(BabaSettings settings) => _settingsRepository.Save(settings);

    public void Start() => _textWatcher.Start();

    public void Dispose() => _textWatcher.Dispose();

    private static Regex? CreateSpeechLinePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var regex = new Regex(
            pattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        if (regex.GetGroupNumbers().Length < 2)
        {
            throw new ArgumentException(
                "SpeechLinePattern must contain a capture group for the speech text.",
                nameof(pattern));
        }

        return regex;
    }
}
