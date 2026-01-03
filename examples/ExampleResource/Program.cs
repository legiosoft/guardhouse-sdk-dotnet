using ExampleResource.Services;
using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGuardhouseResource(options =>
{
    options.Authority = builder.Configuration["Guardhouse:Authority"]!;
    options.Audience = builder.Configuration["Guardhouse:Audience"]!;
    
    var validationMode = builder.Configuration["Guardhouse:ValidationMode"];
    options.ValidationMode = validationMode == "Introspection" 
        ? TokenValidationMode.Introspection 
        : TokenValidationMode.JwtSignature;
    
    if (options.ValidationMode == TokenValidationMode.Introspection)
    {
        options.IntrospectionClientId = builder.Configuration["Guardhouse:IntrospectionClientId"]!;
        options.IntrospectionClientSecret = builder.Configuration["Guardhouse:IntrospectionClientSecret"]!;
    }
    
    options.ValidateIssuer = true;
    options.ValidateAudience = true;
    options.ValidateLifetime = true;
    options.ValidAlgorithms = ["RS256"];
    options.JwksCacheDurationHours = 24;
    options.JwksRefreshIntervalMinutes = 5;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadScope", policy =>
        policy.RequireClaim("scope", "read"));
    
    options.AddPolicy("WriteScope", policy =>
        policy.RequireClaim("scope", "write"));
    
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
