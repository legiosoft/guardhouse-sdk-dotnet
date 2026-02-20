using ExampleResource.Services;
using Guardhouse.SDK.Constants;
using Guardhouse.SDK.Extensions;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(GuardhouseConstants.Authentication.DefaultScheme);

builder.Services.AddGuardhouseResource(options =>
{
    builder.Configuration.GetSection("Guardhouse").Bind(options);
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WriteScope", policy =>
        policy.RequireAssertion(context => HasScope(context.User, "write")));

    options.AddPolicy("AdminRole", policy =>
        policy.RequireRole("admin"));
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

static bool HasScope(ClaimsPrincipal user, string requiredScope)
{
    if (string.IsNullOrWhiteSpace(requiredScope))
    {
        return false;
    }

    var scopes = user.Claims
        .Where(claim => claim.Type == GuardhouseConstants.JwtClaims.Scope || claim.Type == "scp")
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    return scopes.Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
}
