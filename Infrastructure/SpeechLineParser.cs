using System.Text.RegularExpressions;

namespace Baba.Infrastructure;

internal static class SpeechLineParser
{
    private static readonly Regex SpeechLinePattern = new(
        @"^- ....-..-.. ..:..:.. (.+)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static string? Extract(string line)
    {
        var match = SpeechLinePattern.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var message = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }
}
