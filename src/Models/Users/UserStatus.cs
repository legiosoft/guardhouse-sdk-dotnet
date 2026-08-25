namespace Guardhouse.SDK.Models.Users;

using System.Text.Json.Serialization;

[JsonConverter(typeof(UserStatusJsonConverter))]
public enum UserStatus
{
    Unknown = -1,
    Staged = 1,
    Invited = 2,
    Active = 3,
    Archived = 4
}
