using System.Net;
using System.Net.Http.Json;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using FuelFinder.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FuelFinder.Api.Tests;

/// <summary>
/// Each rate-limit test gets its own factory so the in-memory sliding window
/// counter is fresh and tests don't interfere with each other.
/// </summary>
public class RateLimitTests
{
    private static RateLimitFactory CreateFactory() => new();

    private static async Task<Guid> SeedStationAsync(RateLimitFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var station = new Station
        {
            Id        = Guid.NewGuid(),
            Name      = "Rate Limit Station",
            Brand     = "BP",
            Address   = "1 Test Rd",
            Suburb    = "Sydney",
            State     = "NSW",
            Latitude  = -33.8688,
            Longitude = 151.2093,
        };
        db.Stations.Add(station);
        await db.SaveChangesAsync();
        return station.Id;
    }

    [Fact]
    public async Task PostReport_Returns429_AfterLimitExceeded()
    {
        await using var factory = CreateFactory();
        var client    = factory.CreateClient();
        var stationId = await SeedStationAsync(factory);

        object Payload() => new
        {
            stationId,
            status    = "available",
            fuelTypes = new[] { new { fuelType = "ULP", available = true } },
            latitude  = -33.8688,
            longitude = 151.2093,
        };

        var r1 = await client.PostAsJsonAsync("/api/reports", Payload());
        var r2 = await client.PostAsJsonAsync("/api/reports", Payload());
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var r3 = await client.PostAsJsonAsync("/api/reports", Payload());
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
    }

    [Fact]
    public async Task PostReport_Returns429_WithRetryAfterHeader()
    {
        await using var factory = CreateFactory();
        var client    = factory.CreateClient();
        var stationId = await SeedStationAsync(factory);

        object Payload() => new
        {
            stationId,
            status    = "low",
            fuelTypes = new[] { new { fuelType = "Diesel", available = false } },
            latitude  = -33.8688,
            longitude = 151.2093,
        };

        await client.PostAsJsonAsync("/api/reports", Payload());
        await client.PostAsJsonAsync("/api/reports", Payload());
        var rejected = await client.PostAsJsonAsync("/api/reports", Payload());

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"),
            "429 response must include a Retry-After header");
    }
}

public class RateLimitFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "TestDb_RateLimit_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimit:ReportsPerWindow", "2");
        builder.UseSetting("RateLimit:WindowMinutes",    "1");

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().FullName?
                         .Contains("IDbContextOptionsConfiguration") == true &&
                     d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<StationSeeder>();
        });
    }
}
