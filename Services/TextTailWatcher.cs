using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Baba.Services;

public sealed class TextTailWatcher : IDisposable
{
    private readonly string _filePath;
    private readonly FileSystemWatcher _watcher;
    private readonly Regex? _linePattern;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly System.Threading.Timer? _pollTimer;
    private readonly object _syncRoot = new();
    private string? _lastLine;
    private bool _isDisposed;

    public TextTailWatcher(string filePath, Regex? linePattern = null)
    {
        _filePath = Path.GetFullPath(filePath);
        _linePattern = linePattern;
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_filePath)!, Path.GetFileName(_filePath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _debounceTimer = new System.Threading.Timer(ReadLatestLine);
        if (IsUncPath(_filePath))
        {
            _pollTimer = new System.Threading.Timer(_ => ScheduleRead());
        }
    }

    public event EventHandler<string>? LastLineChanged;

    public event EventHandler<Exception>? ReadFailed;

    public void Start()
    {
        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, string.Empty);
        }

        _watcher.EnableRaisingEvents = true;
        _pollTimer?.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        ScheduleRead();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => ScheduleRead();

    private void OnFileRenamed(object sender, RenamedEventArgs e) => ScheduleRead();

    private void ScheduleRead()
    {
        lock (_syncRoot)
        {
            if (!_isDisposed)
            {
                _debounceTimer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void ReadLatestLine(object? state)
    {
        try
        {
            var latestLine = ReadLastNonEmptyLine();
            if (latestLine is not null && latestLine != _lastLine)
            {
                _lastLine = latestLine;
                LastLineChanged?.Invoke(this, latestLine);
            }
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or RegexMatchTimeoutException)
        {
            ReadFailed?.Invoke(this, exception);
        }
    }

    private string? ReadLastNonEmptyLine()
    {
        const int maxAttempts = 3;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);

                string? lastLine = null;
                while (reader.ReadLine() is { } line)
                {
                    var extractedLine = ExtractSpeechLine(line);
                    if (extractedLine is not null)
                    {
                        lastLine = extractedLine;
                    }
                }

                return lastLine;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                Thread.Sleep(100);
            }
        }

        return null;
    }

    private string? ExtractSpeechLine(string line)
    {
        if (_linePattern is null)
        {
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }

        var match = _linePattern.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var message = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _debounceTimer.Dispose();
            _pollTimer?.Dispose();
        }
    }

    private static bool IsUncPath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal);
}
