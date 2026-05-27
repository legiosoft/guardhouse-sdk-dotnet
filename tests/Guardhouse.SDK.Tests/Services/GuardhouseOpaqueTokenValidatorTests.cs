using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Guardhouse.SDK.Models;
using Guardhouse.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Guardhouse.SDK.Tests.Services;

public class GuardhouseOpaqueTokenValidatorTests
{
    [Fact]
    public async Task ValidateTokenAsync_WithInactiveToken_ShouldNotLogWarning()
    {
        var introspectionService = new Mock<IGuardhouseIntrospectionService>();
        introspectionService
            .Setup(x => x.IntrospectTokenAsync("inactive_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntrospectionResponse
            {
                Active = false
            });

        var services = new ServiceCollection();
        services.AddSingleton(introspectionService.Object);
        using var provider = services.BuildServiceProvider();

        var logger = new RecordingLogger<GuardhouseOpaqueTokenValidator>();
        var validator = new GuardhouseOpaqueTokenValidator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new GuardhouseResourceOptions
            {
                Authority = "https://auth.example.com",
                Audience = "api",
                ValidationMode = TokenValidationMode.Introspection
            }),
            logger);

        var result = await validator.ValidateTokenAsync(
            "inactive_token",
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false
            });

        result.IsValid.Should().BeFalse();
        logger.Messages.Should().NotContain(message => message.Level == LogLevel.Warning);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<RecordedMessage> Messages { get; } = [];

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
            Messages.Add(new RecordedMessage(logLevel, formatter(state, exception)));
        }
    }

    private sealed record RecordedMessage(LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
