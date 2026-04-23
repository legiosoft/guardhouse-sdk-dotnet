namespace Guardhouse.SDK.Constants;

public static class GuardhouseApiRoutes
{
    public static class Users
    {
        public const string Collection = "api/v1/users";

        public static string ById(int userId)
        {
            return $"{Collection}/{userId}";
        }

        public static string Password(int userId)
        {
            return $"{ById(userId)}/password";
        }

        public static string Role(int userId, int roleId)
        {
            return $"{ById(userId)}/roles/{roleId}";
        }

        public static string Block(int userId)
        {
            return $"{ById(userId)}/block";
        }

        public static string Unblock(int userId)
        {
            return $"{ById(userId)}/unblock";
        }

        public static string PersonalData(int userId)
        {
            return $"{ById(userId)}/personal-data";
        }
    }

    public static class Roles
    {
        public const string Collection = "api/v1/roles";

        public static string ById(int roleId)
        {
            return $"{Collection}/{roleId}";
        }

        public static string Permissions(int roleId)
        {
            return $"{ById(roleId)}/permissions";
        }
    }

    public static class Permissions
    {
        public const string Collection = "api/v1/permissions";

        public static string ById(int permissionId)
        {
            return $"{Collection}/{permissionId}";
        }
    }
}
