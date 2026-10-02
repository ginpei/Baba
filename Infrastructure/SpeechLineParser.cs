using System.Globalization;
using System.Text.RegularExpressions;

namespace Baba.Infrastructure;

internal static class SpeechLineParser
{
    private static readonly Regex SpeechLinePattern = new(
        @"^(....-..-.. ..:..:..) \| ([^|]+) \| (.+)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static string? Extract(string line)
    {
        var entry = Parse(line);
        return entry?.Message;
    }

    public static SpeechEntry? Parse(string line)
    {
        var match = SpeechLinePattern.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var message = match.Groups[3].Value.Trim();
        if (string.IsNullOrWhiteSpace(match.Groups[2].Value)
            || string.IsNullOrWhiteSpace(message)
            || ContainsUnescapedPipe(message)
            || !DateTime.TryParseExact(
                match.Groups[1].Value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            return null;
        }

        message = message.Replace(@"\|", "|", StringComparison.Ordinal);
        return new SpeechEntry(message, timestamp);
    }

    private static bool ContainsUnescapedPipe(string message)
    {
        for (var index = 0; index < message.Length; index++)
        {
            if (message[index] == '|' && (index == 0 || message[index - 1] != '\\'))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed record SpeechEntry(string Message, DateTime Timestamp);
