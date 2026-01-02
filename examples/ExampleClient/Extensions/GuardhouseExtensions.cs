using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;

namespace ExampleClient.Extensions;

public static class GuardhouseExtensions
{
    public static void AddGuardhouse(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGuardhouseClient(options =>
        {
            options.Authority = configuration["Guardhouse:Authority"]!;
            options.ClientId = configuration["Guardhouse:ClientId"]!;
            options.ClientSecret = configuration["Guardhouse:ClientSecret"]!;
            options.Scope = configuration["Guardhouse:Scope"]!;
        });
    }
}
