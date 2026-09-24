namespace Baba.Configuration;

public sealed class SpeechSourceSettings
{
    public string SpeechFilePath { get; init; } = string.Empty;

    public string? SpeechLinePattern { get; init; }
}
