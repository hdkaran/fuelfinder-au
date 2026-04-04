using System.Threading.RateLimiting;
using Azure.Identity;
using FuelFinder.Api.Data;
using FuelFinder.Api.Endpoints;
using FuelFinder.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// In production the App Service injects secrets via Key Vault references in app settings
// (e.g. @Microsoft.KeyVault(VaultName=fuelfinder-kv;SecretName=SqlConnectionString)).
// For local dev with managed identity, set "KeyVault:Uri" in appsettings.Development.json.
var kvUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(kvUri))
    builder.Configuration.AddAzureKeyVault(new Uri(kvUri), new DefaultAzureCredential());

// Database — Scoped factory enables PriceSyncService to create independent DbContext
// instances per state sync; Scoped lifetime avoids singleton/scoped lifetime mismatch.
builder.Services.AddDbContextFactory<AppDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")),
    lifetime: ServiceLifetime.Scoped);

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

// Rate limiting — sliding window, keyed by client IP
var permitLimit   = builder.Configuration.GetValue<int>("RateLimit:ReportsPerWindow", 5);
var windowMinutes = builder.Configuration.GetValue<int>("RateLimit:WindowMinutes",    10);

builder.Services.AddRateLimiter(options =>
{
    // Sliding window keyed by client IP — honours X-Forwarded-For from Azure App Service proxy
    options.AddPolicy<string>("reports", httpContext =>
    {
        var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit          = permitLimit,
            Window               = TimeSpan.FromMinutes(windowMinutes),
            SegmentsPerWindow    = 5,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0,
        });
    });

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.Headers.RetryAfter =
            ((int)TimeSpan.FromMinutes(windowMinutes).TotalSeconds).ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many reports. Please wait a few minutes before submitting again." },
            token);
    };
});

// Services
builder.Services.AddHttpClient("FuelCheck");
builder.Services.AddHttpClient("FuelWatch");
builder.Services.AddHttpClient("FuelPricesQld");
builder.Services.AddHttpClient("SaFuelPrices");
builder.Services.AddScoped<StationQueryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<StationSeeder>();
builder.Services.AddScoped<WaStationSeeder>();
builder.Services.AddScoped<QldStationSeeder>();
builder.Services.AddScoped<SaStationSeeder>();
builder.Services.AddScoped<PushService>();
builder.Services.AddScoped<IPriceSyncService, PriceSyncService>();
builder.Services.AddHostedService<PriceSyncBackgroundService>();

var app = builder.Build();

// Seed stations on first boot — each seeder checks its own state independently
using (var scope = app.Services.CreateScope())
{
    var nswSeeder = scope.ServiceProvider.GetService<StationSeeder>();
    if (nswSeeder is not null)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<StationSeeder>>();
        try { await nswSeeder.SeedAsync(); }
        catch (Exception ex) { log.LogError(ex, "NSW station seeding failed."); }
    }

    var waSeeder = scope.ServiceProvider.GetService<WaStationSeeder>();
    if (waSeeder is not null)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<WaStationSeeder>>();
        try { await waSeeder.SeedAsync(); }
        catch (Exception ex) { log.LogError(ex, "WA station seeding failed."); }
    }

    var qldSeeder = scope.ServiceProvider.GetService<QldStationSeeder>();
    if (qldSeeder is not null)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<QldStationSeeder>>();
        try { await qldSeeder.SeedAsync(); }
        catch (Exception ex) { log.LogError(ex, "QLD station seeding failed."); }
    }

    var saSeeder = scope.ServiceProvider.GetService<SaStationSeeder>();
    if (saSeeder is not null)
    {
        var log = scope.ServiceProvider.GetRequiredService<ILogger<SaStationSeeder>>();
        try { await saSeeder.SeedAsync(); }
        catch (Exception ex) { log.LogError(ex, "SA station seeding failed."); }
    }
}

app.UseCors();
app.UseRateLimiter();

// Endpoints
var api = app.MapGroup("/api");
api.MapStationEndpoints();
api.MapReportEndpoints();
api.MapStatsEndpoints();
api.MapPushEndpoints();
api.MapPriceEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Expose Program to WebApplicationFactory in integration tests
public partial class Program { }
