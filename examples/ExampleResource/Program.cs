using ExampleResource.Services;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Guardhouse Example Resource API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"token\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

builder.Services.AddAuthentication(GuardhouseConstants.Authentication.DefaultScheme);

builder.Services.AddGuardhouseResource(options =>
{
    var guardhouseSection = builder.Configuration.GetSection("Guardhouse");

    options.Authority = guardhouseSection["Authority"] ?? string.Empty;
    options.Audience = guardhouseSection["Audience"] ?? string.Empty;
    options.IntrospectionClientId = guardhouseSection["IntrospectionClientId"];
    options.IntrospectionClientSecret = guardhouseSection["IntrospectionClientSecret"];

    if (Enum.TryParse<TokenValidationMode>(guardhouseSection["ValidationMode"], true, out var validationMode))
    {
        options.ValidationMode = validationMode;
    }

    if (Enum.TryParse<IntrospectionCredentialTransmission>(guardhouseSection["IntrospectionCredentialTransmission"], true, out var credentialTransmission))
    {
        options.IntrospectionCredentialTransmission = credentialTransmission;
    }

    var requireHttps = guardhouseSection.GetValue<bool?>("RequireHttps");
    if (requireHttps.HasValue)
    {
        options.RequireHttps = requireHttps.Value;
    }

    var requestTimeoutSeconds = guardhouseSection.GetValue<int?>("RequestTimeoutSeconds");
    if (requestTimeoutSeconds.HasValue)
    {
        options.RequestTimeoutSeconds = requestTimeoutSeconds.Value;
    }

    var enableDebug = guardhouseSection.GetValue<bool?>("EnableDebug");
    if (enableDebug.HasValue)
    {
        options.EnableDebug = enableDebug.Value;
        if (enableDebug.Value)
        {
            IdentityModelEventSource.ShowPII = true;
        }
    }
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationConsts.Policies.SA, policy =>
        policy.RequireAssertion(context => IsSystemAdministrator(context.User)));
});

builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Guardhouse Example Resource v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
static bool IsSystemAdministrator(ClaimsPrincipal user)
{
    return user.FindAll(AuthorizationConsts.ClaimTypes.System)
        .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Any(claimValue => string.Equals(
            claimValue,
            AuthorizationConsts.ClaimValues.SystemAdministrator,
            StringComparison.OrdinalIgnoreCase));
}
