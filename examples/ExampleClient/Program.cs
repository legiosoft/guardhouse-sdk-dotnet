using Guardhouse.SDK.Extensions;
using Guardhouse.SDK.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = builder.Configuration["Guardhouse:Authority"]!;
    options.ClientId = builder.Configuration["Guardhouse:ClientId"]!;
    options.ClientSecret = builder.Configuration["Guardhouse:ClientSecret"]!;
    options.Scope = builder.Configuration["Guardhouse:Scope"]!;
    options.IntrospectionClientId = builder.Configuration["Guardhouse:ClientId"]!;
    options.IntrospectionClientSecret = builder.Configuration["Guardhouse:ClientSecret"]!;
    options.IntrospectionCredentialTransmission = IntrospectionCredentialTransmission.FormData;
    options.EnableTokenCaching = true;
    options.EnableTokenRefresh = true;
    options.IncludeOfflineAccessScope = true;
    options.EnableHttpResilience = true;
});

builder.Services.AddScoped<ExampleClient.Services.IProductService, ExampleClient.Services.ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Guardhouse Example Client v1");
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
