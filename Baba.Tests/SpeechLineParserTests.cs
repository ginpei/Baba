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
    [InlineData("- 2026-99-99 17:30:01 hello")]
    public void Extract_ReturnsNullWhenLineDoesNotMatchFixedFormat(string line)
    {
        Assert.Null(SpeechLineParser.Extract(line));
    }

    [Fact]
    public void Parse_ReturnsTimestampAndMessage()
    {
        var entry = SpeechLineParser.Parse("- 2026-09-23 17:30:01 hello");

        Assert.NotNull(entry);
        Assert.Equal("hello", entry.Message);
        Assert.Equal(new DateTime(2026, 9, 23, 17, 30, 1), entry.Timestamp);
    }
}
