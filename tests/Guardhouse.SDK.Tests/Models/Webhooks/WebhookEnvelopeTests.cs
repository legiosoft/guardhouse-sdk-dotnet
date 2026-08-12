using System.Text.Json;
using FluentAssertions;
using Guardhouse.SDK.Enums;
using Guardhouse.SDK.Models.Webhooks;
using Xunit;

namespace Guardhouse.SDK.Tests.Models.Webhooks;

public class WebhookEnvelopeTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void WebhookEventType_UserActivated_ShouldMatchApiContract()
    {
        ((int)WebhookEventType.UserActivated).Should().Be(3);
    }

    [Fact]
    public void Deserialize_UserActivatedWebhookEnvelope_ShouldReadPayload()
    {
        const string json = """
                            {
                              "eventType": 3,
                              "data": {
                                "userId": 123,
                                "email": "user@example.com",
                                "firstName": "John",
                                "lastName": "Doe"
                              },
                              "timestamp": 1234567890
                            }
                            """;

        var envelope = JsonSerializer.Deserialize<WebhookEnvelope<UserActivatedWebhookPayload>>(json, WebOptions);

        envelope.Should().NotBeNull();
        envelope!.EventType.Should().Be(WebhookEventType.UserActivated);
        envelope.Timestamp.Should().Be(1234567890);
        envelope.Data.UserId.Should().Be(123);
        envelope.Data.Email.Should().Be("user@example.com");
        envelope.Data.FirstName.Should().Be("John");
        envelope.Data.LastName.Should().Be("Doe");
    }
}
