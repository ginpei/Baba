using System.IO;
using System.Text;

namespace Baba.Infrastructure;

internal sealed class ApplicationLog
{
    private readonly string _filePath;
    private readonly object _syncRoot = new();

    public ApplicationLog(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "baba.log");
    }

    internal string FilePath => _filePath;

    public void WriteEvent(string eventName, string? details = null)
    {
        var message = details is null
            ? eventName
            : $"{eventName}: {details}";
        Write("INFO", message);
    }

    public void WriteException(string context, Exception exception)
    {
        Write("ERROR", $"{context}{Environment.NewLine}{exception}");
    }

    private void Write(string level, string message)
    {
        try
        {
            var entry = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
            lock (_syncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.AppendAllText(_filePath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never cause the application to fail.
        }
    }
}
