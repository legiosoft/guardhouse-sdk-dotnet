using Guardhouse.SDK.Extensions;

namespace ExampleClient.Extensions;

public static class GuardhouseExtensions
{
    public static void AddCustomGuardhouseClient(this IServiceCollection services, IConfiguration configuration)
    {
        var guardhouseSection = configuration.GetSection("Guardhouse");
        
        services.AddGuardhouseClient(options =>
        {
            options.Authority = guardhouseSection["Authority"]!;
            options.ClientId = guardhouseSection["ClientId"]!;
            options.ClientSecret = guardhouseSection["ClientSecret"]!;
            options.Scope = guardhouseSection["Scope"]!;
        });
    }

    public static void AddCustomGuardhouseResource(this IServiceCollection services, IConfiguration configuration)
    {
        var guardhouseSection = configuration.GetSection("Guardhouse");
        
        services.AddGuardhouseResource(options =>
        {
            options.Authority = guardhouseSection["Authority"]!;
            options.Audience = guardhouseSection["Audience"];
        });
    }

    public static void AddCustomGuardhouse(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCustomGuardhouseClient(configuration);
        services.AddCustomGuardhouseResource(configuration);
    }
}
