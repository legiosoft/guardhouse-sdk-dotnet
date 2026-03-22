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
            if (reader.TryGetInt32(out var numericValue) && Enum.IsDefined(typeof(UserStatus), numericValue))
            {
                return (UserStatus)numericValue;
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

            if (int.TryParse(value, out var numericValue) && Enum.IsDefined(typeof(UserStatus), numericValue))
            {
                return (UserStatus)numericValue;
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
            UserStatus.Active => "active",
            UserStatus.Inactive => "inactive",
            UserStatus.Locked => "locked",
            _ => "unknown"
        });
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

        if (string.Equals(normalizedValue, "active", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Active;
            return true;
        }

        if (string.Equals(normalizedValue, "inactive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Inactive;
            return true;
        }

        if (string.Equals(normalizedValue, "locked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "lockedout", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedValue, "suspended", StringComparison.OrdinalIgnoreCase))
        {
            status = UserStatus.Locked;
            return true;
        }

        status = UserStatus.Unknown;
        return false;
    }
}
