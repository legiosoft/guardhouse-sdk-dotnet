using System.IdentityModel.Tokens.Jwt;

namespace ExampleClient.Utilities;

public static class TokenScopeExtensions
{
    public static string? GetTokenScope(this string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(c => c.Type == "scope")?.Value
                ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "scp")?.Value;
        }
        catch
        {
            return null;
        }
    }
}
