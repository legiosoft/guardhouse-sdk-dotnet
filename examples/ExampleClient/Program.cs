using ExampleClient.Models;
using ExampleClient.Services;
using Guardhouse.SDK.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerUI();

builder.Services.AddGuardhouseClient(options =>
{
    options.Authority = builder.Configuration["Guardhouse:Authority"]!;
    options.ClientId = builder.Configuration["Guardhouse:ClientId"]!;
    options.ClientSecret = builder.Configuration["Guardhouse:ClientSecret"]!;
    options.Scope = builder.Configuration["Guardhouse:Scope"]!;
    options.EnableTokenCaching = true;
    options.EnableTokenRefresh = true;
    options.EnableHttpResilience = true;
});

builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Guardhouse Example API v1");
    });
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
