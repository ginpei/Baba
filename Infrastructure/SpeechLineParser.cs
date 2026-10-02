using System.Globalization;
using System.Text.RegularExpressions;

namespace Baba.Infrastructure;

internal static class SpeechLineParser
{
    private static readonly Regex SpeechLinePattern = new(
        @"^- (....-..-.. ..:..:..) (.+)$",
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

        var message = match.Groups[2].Value.Trim();
        if (string.IsNullOrWhiteSpace(message)
            || !DateTime.TryParseExact(
                match.Groups[1].Value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            return null;
        }

        return new SpeechEntry(message, timestamp);
    }
}

internal sealed record SpeechEntry(string Message, DateTime Timestamp);
