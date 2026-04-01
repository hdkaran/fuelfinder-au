using System.Net;
using System.Net.Http.Json;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuelFinder.Api.Tests;

public class ReportEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly object ValidPayload = new
    {
        stationId = Guid.Empty, // overridden per-test
        status = "low",
        fuelTypes = new[] { new { fuelType = "Diesel", available = true } },
        latitude = -33.8688,
        longitude = 151.2093,
    };

    private async Task<Guid> SeedStationAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Test Station",
            Brand = "Shell",
            Address = "2 Test St",
            Suburb = "Melbourne",
            State = "VIC",
            Latitude = -37.8136,
            Longitude = 144.9631,
        };
        db.Stations.Add(station);
        await db.SaveChangesAsync();
        return station.Id;
    }

    // ── POST /api/reports ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostReport_Returns201_WhenValid()
    {
        var stationId = await SeedStationAsync();
        var payload = new
        {
            stationId,
            status = "available",
            fuelTypes = new[] { new { fuelType = "ULP", available = true } },
            latitude = -37.8136,
            longitude = 144.9631,
        };

        var response = await _client.PostAsJsonAsync("/api/reports", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostReport_Returns400_WhenStatusInvalid()
    {
        var stationId = await SeedStationAsync();
        var payload = new
        {
            stationId,
            status = "wrong",
            fuelTypes = new[] { new { fuelType = "ULP", available = true } },
            latitude = -37.8136,
            longitude = 144.9631,
        };

        var response = await _client.PostAsJsonAsync("/api/reports", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReport_Returns400_WhenFuelTypeInvalid()
    {
        var stationId = await SeedStationAsync();
        var payload = new
        {
            stationId,
            status = "low",
            fuelTypes = new[] { new { fuelType = "Petrol98", available = true } },
            latitude = -37.8136,
            longitude = 144.9631,
        };

        var response = await _client.PostAsJsonAsync("/api/reports", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReport_Returns404_WhenStationNotFound()
    {
        var payload = new
        {
            stationId = Guid.NewGuid(),
            status = "out",
            fuelTypes = new[] { new { fuelType = "E10", available = false } },
            latitude = -37.8136,
            longitude = 144.9631,
        };

        var response = await _client.PostAsJsonAsync("/api/reports", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("available")]
    [InlineData("low")]
    [InlineData("out")]
    [InlineData("queue")]
    public async Task PostReport_AcceptsAllValidStatuses(string status)
    {
        var stationId = await SeedStationAsync();
        var payload = new
        {
            stationId,
            status,
            fuelTypes = new[] { new { fuelType = "Premium", available = true } },
            latitude = -37.8136,
            longitude = 144.9631,
        };

        var response = await _client.PostAsJsonAsync("/api/reports", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── GET /api/reports/recent?stationId= ────────────────────────────────────

    [Fact]
    public async Task GetRecent_ReturnsReports_AfterSubmission()
    {
        var stationId = await SeedStationAsync();
        var payload = new
        {
            stationId,
            status = "low",
            fuelTypes = new[] { new { fuelType = "Diesel", available = false } },
            latitude = -37.8136,
            longitude = 144.9631,
        };
        await _client.PostAsJsonAsync("/api/reports", payload);

        var response = await _client.GetAsync($"/api/reports/recent?stationId={stationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reports = await response.Content.ReadFromJsonAsync<List<ReportDto>>();
        Assert.NotNull(reports);
        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.Equal("low", r.Status));
    }

    [Fact]
    public async Task GetRecent_ReturnsEmpty_WhenNoReports()
    {
        var stationId = await SeedStationAsync();

        var response = await _client.GetAsync($"/api/reports/recent?stationId={stationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reports = await response.Content.ReadFromJsonAsync<List<ReportDto>>();
        Assert.NotNull(reports);
        Assert.Empty(reports);
    }
}
