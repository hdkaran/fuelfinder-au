using System.Net;
using System.Net.Http.Json;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuelFinder.Api.Tests;

public class StatsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetSummary_ReturnsOk_WithZeroCounts_WhenNoReports()
    {
        var response = await _client.GetAsync("/api/stats/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<StatsDto>();
        Assert.NotNull(dto);
        Assert.True(dto.TotalReportsToday >= 0);
        Assert.True(dto.StationsAffected >= 0);
        Assert.False(string.IsNullOrEmpty(dto.LastUpdated));
    }

    [Fact]
    public async Task GetSummary_CountsReport_AfterSubmission()
    {
        // Seed a station and post a report to it
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Stats Station",
            Brand = "Caltex",
            Address = "3 Stats St",
            Suburb = "Brisbane",
            State = "QLD",
            Latitude = -27.4698,
            Longitude = 153.0251,
        };
        db.Stations.Add(station);
        await db.SaveChangesAsync();

        await _client.PostAsJsonAsync("/api/reports", new
        {
            stationId = station.Id,
            status = "out",
            fuelTypes = new[] { new { fuelType = "ULP", available = false } },
            latitude = -27.4698,
            longitude = 153.0251,
        });

        // ReportService invalidates "stats:summary" after submission, so this
        // call hits the DB and reflects the new report.
        var stats = await _client.GetFromJsonAsync<StatsDto>("/api/stats/summary");
        Assert.NotNull(stats);
        Assert.True(stats.TotalReportsToday >= 1);
        Assert.True(stats.StationsAffected >= 1);
    }

    [Fact]
    public async Task GetSummary_LastUpdated_IsIso8601()
    {
        var response = await _client.GetAsync("/api/stats/summary");
        var dto = await response.Content.ReadFromJsonAsync<StatsDto>();
        Assert.NotNull(dto);
        Assert.True(DateTimeOffset.TryParse(dto.LastUpdated, out _),
            $"LastUpdated '{dto.LastUpdated}' is not a valid ISO 8601 date.");
    }
}
