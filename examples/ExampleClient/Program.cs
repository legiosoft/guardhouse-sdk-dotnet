using ExampleClient.Extensions;
using ExampleClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCustomSwaggerGen();

builder.Services.AddCustomAuthorization();

builder.Services.AddCustomGuardhouse(builder.Configuration);

builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Guardhouse Example API v1");
        options.OAuthClientId(builder.Configuration["Guardhouse:ClientId"]);
        options.OAuthUsePkce();
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
