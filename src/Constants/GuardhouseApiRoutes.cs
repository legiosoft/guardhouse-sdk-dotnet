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

        public static string Roles(int userId)
        {
            return $"{ById(userId)}/roles";
        }

        public static string Permissions(int userId)
        {
            return $"{ById(userId)}/permissions";
        }

        public static string PersonalData(int userId)
        {
            return $"{ById(userId)}/personal-data";
        }

        public static string Anonymize(int userId)
        {
            return $"{ById(userId)}/anonymize";
        }
    }
}
