using Azure.Identity;
using FuelFinder.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// In production the App Service injects secrets via Key Vault references in app settings
// (e.g. @Microsoft.KeyVault(VaultName=fuelfinder-kv;SecretName=SqlConnectionString)).
// For local dev with managed identity, set "KeyVault:Uri" in appsettings.Development.json.
var kvUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(kvUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), new DefaultAzureCredential());
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
