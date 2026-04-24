using FluentAssertions;
using Guardhouse.SDK.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Guardhouse.SDK.Tests.Extensions;

public class LoggerExtensionsTests
{
    [Fact]
    public void LogDebugIf_ShouldEscapeControlCharactersInTemplateAndStringArguments()
    {
        var logger = new RecordingLogger();

        logger.LogDebugIf(
            true,
            "User input:\n{Value}",
            "first\r\nsecond\tthird\u2028fourth\u0001");

        logger.Messages.Should().ContainSingle()
            .Which.Should().Be(@"User input:\nfirst\r\nsecond\tthird\u2028fourth\u0001");
    }

    [Fact]
    public void LogDebugIf_ShouldKeepNonStringArgumentTypes()
    {
        var logger = new RecordingLogger();

        logger.LogDebugIf(true, "Attempt {Attempt} active {Active}", 3, true);

        logger.Messages.Should().ContainSingle()
            .Which.Should().Be("Attempt 3 active True");
    }

    [Fact]
    public void LogDebugIf_ShouldNotLogWhenConditionIsFalse()
    {
        var logger = new RecordingLogger();

        logger.LogDebugIf(false, "Message {Value}", "value");

        logger.Messages.Should().BeEmpty();
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
