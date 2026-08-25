namespace Guardhouse.SDK.Models.Users;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class UserStatusJsonConverter : JsonConverter<UserStatus>
{
    public override UserStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var numericValue) &&
                TryMapNumericStatus(numericValue, out var status))
            {
                return status;
            }

            return UserStatus.Unknown;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return UserStatus.Unknown;
            }

            if (int.TryParse(value, out var numericValue) &&
                TryMapNumericStatus(numericValue, out var status))
            {
                return status;
            }

            if (TryMapStringStatus(value, out var mappedStatus))
            {
                return mappedStatus;
            }

            return UserStatus.Unknown;
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return UserStatus.Unknown;
        }

        throw new JsonException($"Unable to parse user status from token type '{reader.TokenType}'.");
    }

    public override void Write(Utf8JsonWriter writer, UserStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            UserStatus.Staged => "staged",
            UserStatus.Invited => "invited",
            UserStatus.Active => "active",
            UserStatus.Archived => "archived",
            _ => "unknown"
        });
    }

    private static bool TryMapNumericStatus(int value, out UserStatus status)
    {
        if (Enum.IsDefined(typeof(UserStatus), value))
        {
            status = (UserStatus)value;
            return true;
        }

        status = UserStatus.Unknown;
        return false;
    }

    private static bool TryMapStringStatus(string value, out UserStatus status)
    {
        if (Enum.TryParse<UserStatus>(value, ignoreCase: true, out status))
        {
            return true;
        }

        var normalizedValue = value
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.Equals(normalizedValue, "staged", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Staged;
            return true;
        }

        if (string.Equals(normalizedValue, "invited", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Invited;
            return true;
        }

        if (string.Equals(normalizedValue, "active", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Active;
            return true;
        }

        if (string.Equals(normalizedValue, "archived", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Archived;
            return true;
        }

        status = UserStatus.Unknown;
        return false;
    }
}
