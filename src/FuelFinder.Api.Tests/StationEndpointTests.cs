using System.Net;
using System.Net.Http.Json;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuelFinder.Api.Tests;

public class StationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Seed helper ────────────────────────────────────────────────────────────

    private static Station MakeStation(double lat = -33.8688, double lng = 151.2093) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Station",
        Brand = "BP",
        Address = "1 Test St",
        Suburb = "Sydney",
        State = "NSW",
        Latitude = lat,
        Longitude = lng,
    };

    private async Task<Station> SeedAsync(Station station)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Stations.Add(station);
        await db.SaveChangesAsync();
        return station;
    }

    // ── /api/stations/nearby ───────────────────────────────────────────────────

    [Fact]
    public async Task GetNearby_ReturnsStation_WhenWithinRadius()
    {
        var station = await SeedAsync(MakeStation());

        // Sydney CBD coords, 5 km radius — station is at the exact same point
        var response = await _client.GetAsync(
            "/api/stations/nearby?lat=-33.8688&lng=151.2093&radius=5000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stations = await response.Content.ReadFromJsonAsync<List<StationDto>>();
        Assert.NotNull(stations);
        Assert.Contains(stations, s => s.Id == station.Id);
    }

    [Fact]
    public async Task GetNearby_ExcludesStation_WhenOutsideRadius()
    {
        // Use unique coords to avoid cache collision with other nearby tests.
        // Station is placed ~50 km south; query is centred further north.
        var station = await SeedAsync(MakeStation(lat: -34.3, lng: 150.5));

        var response = await _client.GetAsync(
            "/api/stations/nearby?lat=-33.5&lng=150.5&radius=5000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stations = await response.Content.ReadFromJsonAsync<List<StationDto>>();
        Assert.NotNull(stations);
        Assert.DoesNotContain(stations, s => s.Id == station.Id);
    }

    [Fact]
    public async Task GetNearby_ReturnsMissingParams_WhenLatOmitted()
    {
        var response = await _client.GetAsync(
            "/api/stations/nearby?lng=151.2093&radius=5000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetNearby_ReturnsEmptyList_WhenNoStations()
    {
        var response = await _client.GetAsync(
            "/api/stations/nearby?lat=-27.0&lng=153.0&radius=1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stations = await response.Content.ReadFromJsonAsync<List<StationDto>>();
        Assert.NotNull(stations);
        Assert.Empty(stations);
    }

    // ── /api/stations/{id} ────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsStation_WhenExists()
    {
        var station = await SeedAsync(MakeStation());

        var response = await _client.GetAsync($"/api/stations/{station.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<StationDto>();
        Assert.NotNull(dto);
        Assert.Equal(station.Id, dto.Id);
        Assert.Equal(station.Name, dto.Name);
    }

    [Fact]
    public async Task GetById_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync($"/api/stations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Returns404_WhenIdIsNotGuid()
    {
        // The route constraint {id:guid} causes a non-GUID path to produce no
        // route match, which ASP.NET Core surfaces as 404 (not 400).
        var response = await _client.GetAsync("/api/stations/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
