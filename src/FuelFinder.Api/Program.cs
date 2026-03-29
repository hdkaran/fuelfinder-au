using Azure.Identity;
using FuelFinder.Api.Data;
using FuelFinder.Api.Endpoints;
using FuelFinder.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// In production the App Service injects secrets via Key Vault references in app settings
// (e.g. @Microsoft.KeyVault(VaultName=fuelfinder-kv;SecretName=SqlConnectionString)).
// For local dev with managed identity, set "KeyVault:Uri" in appsettings.Development.json.
var kvUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(kvUri))
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), new DefaultAzureCredential());

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")));

// Distributed cache — Redis in production, in-memory fallback for local dev
var redisConn = builder.Configuration.GetConnectionString("RedisConnection");
if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
else
    builder.Services.AddDistributedMemoryCache();

// CORS — allows the Static Web Apps frontend to call the API cross-origin
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

// Services
builder.Services.AddHttpClient("FuelCheck");
builder.Services.AddScoped<StationQueryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<StationSeeder>();

var app = builder.Build();

// Seed stations from NSW FuelCheck on first boot (skipped in test environments that remove StationSeeder)
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetService<StationSeeder>();
    if (seeder is not null)
    {
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<StationSeeder>>();
        try { await seeder.SeedAsync(); }
        catch (Exception ex) { seedLogger.LogError(ex, "Station seeding failed — API will start without station data."); }
    }
}

app.UseCors();

// Endpoints
var api = app.MapGroup("/api");
api.MapStationEndpoints();
api.MapReportEndpoints();
api.MapStatsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Expose Program to WebApplicationFactory in integration tests
public partial class Program { }
