namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

[JsonConverter(typeof(UserStatusJsonConverter))]
public enum UserStatus
{
    Unknown = -1,
    Staged = 0,
    Active = 1,
    Inactive = 2,
    Locked = 3
}
