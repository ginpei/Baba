using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class SpeechLineParserTests
{
    [Theory]
    [InlineData("- 2026-09-23 17:30:01 hello", "hello")]
    [InlineData("- 2026-09-23 17:30:01  hello  ", "hello")]
    public void Extract_ReturnsTrimmedMessageFromFixedFormat(string line, string expected)
    {
        Assert.Equal(expected, SpeechLineParser.Extract(line));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("- 2026-09-23 17:30:01   ")]
    [InlineData(" - 2026-09-23 17:30:01 hello")]
    [InlineData("- 2026/09/23 17:30:01 hello")]
    public void Extract_ReturnsNullWhenLineDoesNotMatchFixedFormat(string line)
    {
        Assert.Null(SpeechLineParser.Extract(line));
    }
}
