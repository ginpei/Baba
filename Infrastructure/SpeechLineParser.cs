using System.Text.RegularExpressions;

namespace Baba.Infrastructure;

internal static class SpeechLineParser
{
    public static Regex? CreatePattern(string? pattern)
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

    public static string? Extract(string line, Regex? pattern)
    {
        if (pattern is null)
        {
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }

        var match = pattern.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var message = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
}
