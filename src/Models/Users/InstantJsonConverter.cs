namespace Guardhouse.SDK.Models.Users;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

public sealed class InstantJsonConverter : JsonConverter<Instant?>
{
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unable to parse Instant from token type '{reader.TokenType}'.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parseResult = InstantPattern.ExtendedIso.Parse(value);
        if (!parseResult.Success)
        {
            throw new JsonException($"Unable to parse Instant value '{value}'.");
        }

        return parseResult.Value;
    }

    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(InstantPattern.ExtendedIso.Format(value.Value));
    }
}
