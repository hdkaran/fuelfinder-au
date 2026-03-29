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

// Services
builder.Services.AddScoped<StationQueryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<StatsService>();

var app = builder.Build();

// Endpoints
var api = app.MapGroup("/api");
api.MapStationEndpoints();
api.MapReportEndpoints();
api.MapStatsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
