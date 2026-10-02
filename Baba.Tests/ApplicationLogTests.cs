using Baba.Infrastructure;

namespace Baba.Tests;

public sealed class ApplicationLogTests
{
    [Fact]
    public void WriteEvent_AppendsTimestampedEntry()
    {
        using var directory = new TemporaryDirectory();
        var log = new ApplicationLog(directory.Path);

        log.WriteEvent("Application started", "test details");

        var contents = File.ReadAllText(log.FilePath);
        Assert.Contains("[INFO] Application started: test details", contents);
    }

    [Fact]
    public void WriteException_AppendsContextAndExceptionDetails()
    {
        using var directory = new TemporaryDirectory();
        var log = new ApplicationLog(directory.Path);
        var exception = new InvalidOperationException("test failure");

        log.WriteException("Test operation failed", exception);

        var contents = File.ReadAllText(log.FilePath);
        Assert.Contains("[ERROR] Test operation failed", contents);
        Assert.Contains("System.InvalidOperationException: test failure", contents);
    }
}
