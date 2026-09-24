using System.Text.RegularExpressions;
using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class SpeechLineParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void CreatePattern_ReturnsNullForBlankPattern(string? pattern)
    {
        Assert.Null(SpeechLineParser.CreatePattern(pattern));
    }

    [Fact]
    public void CreatePattern_RequiresCaptureGroup()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SpeechLineParser.CreatePattern("^say:.*$"));

        Assert.Equal("pattern", exception.ParamName);
    }

    [Fact]
    public void CreatePattern_UsesCultureInvariantMatchingAndTimeout()
    {
        var pattern = SpeechLineParser.CreatePattern("^say: (.+)$");

        Assert.NotNull(pattern);
        Assert.True(pattern.Options.HasFlag(RegexOptions.CultureInvariant));
        Assert.Equal(TimeSpan.FromMilliseconds(100), pattern.MatchTimeout);
    }

    [Fact]
    public void CreatePattern_PropagatesInvalidRegex()
    {
        Assert.Throws<RegexParseException>(() => SpeechLineParser.CreatePattern("("));
    }

    [Theory]
    [InlineData(" hello ", "hello")]
    [InlineData(" \t ", null)]
    public void Extract_TrimsUnpatternedLineAndIgnoresBlankLines(string line, string? expected)
    {
        Assert.Equal(expected, SpeechLineParser.Extract(line, null));
    }

    [Fact]
    public void Extract_ReturnsTrimmedCapture()
    {
        var pattern = SpeechLineParser.CreatePattern(@"^say: (.+)$");

        Assert.Equal("hello", SpeechLineParser.Extract("say:  hello  ", pattern));
    }

    [Theory]
    [InlineData("ignore: hello")]
    [InlineData("say:   ")]
    public void Extract_ReturnsNullWhenPatternDoesNotProduceSpeech(string line)
    {
        var pattern = SpeechLineParser.CreatePattern(@"^say: (.*)$");

        Assert.Null(SpeechLineParser.Extract(line, pattern));
    }
}
