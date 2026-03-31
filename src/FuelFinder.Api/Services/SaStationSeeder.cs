using System.Text.Json;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

/// <summary>
/// One-time seeder that populates SA stations from the SA Fuel Pricing Information API
/// (https://fppdirectapi-prod.safuelpricinginformation.com.au). Requires a subscriber token
/// obtained by registering at https://www.safuelpricinginformation.com.au.
/// Runs only when no SA stations exist — safe to deploy alongside other state data.
/// </summary>
public class SaStationSeeder(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SaStationSeeder> logger)
{
    private const string ApiBase       = "https://fppdirectapi-prod.safuelpricinginformation.com.au";
    private const int    CountryId     = 21;   // Australia
    private const int    StateRegionId = 4;    // SA at geo level 3

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var token = config["SaFuelPricing:SubscriberToken"];
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("SaFuelPricing:SubscriberToken not configured — skipping SA seed.");
            return;
        }

        if (await db.Stations.AnyAsync(s => s.State == "SA", ct))
        {
            logger.LogInformation("SA stations already present — skipping SA seed.");
            return;
        }

        logger.LogInformation("Seeding SA stations from SA Fuel Pricing Information API…");

        var client = httpClientFactory.CreateClient("SaFuelPricing");

        // Fetch lookup tables in parallel — brands and suburb-level geographic regions
        var (brands, regions) = await FetchLookupsAsync(client, token, ct);

        // Fetch all SA site details (geoRegionLevel=3 = state, geoRegionId=4 = SA)
        var siteUrl = $"{ApiBase}/Subscriber/GetFullSiteDetails" +
                      $"?countryId={CountryId}&geoRegionLevel=3&geoRegionId={StateRegionId}";

        HttpResponseMessage response;
        try
        {
            response = await SendAsync(client, token, siteUrl, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch SA site details from FPP API.");
            return;
        }

        var body       = await response.Content.ReadAsStringAsync(ct);
        var siteResult = JsonSerializer.Deserialize<GetFullSiteDetailsResponse>(body, JsonOpts);

        if (siteResult?.S is null or { Count: 0 })
        {
            logger.LogWarning("FPP API returned no SA stations.");
            return;
        }

        var stations = siteResult.S
            .Where(s => s.Lat != 0 && s.Lng != 0 && !string.IsNullOrWhiteSpace(s.N))
            .Select(s => new Station
            {
                Id        = Guid.NewGuid(),
                Name      = s.N.Trim(),
                Brand     = brands.GetValueOrDefault(s.B, "Independent"),
                Address   = s.A.Trim(),
                Suburb    = regions.GetValueOrDefault(s.G1, string.Empty),
                State     = "SA",
                Latitude  = s.Lat,
                Longitude = s.Lng,
            })
            .ToList();

        if (stations.Count == 0)
        {
            logger.LogWarning("No valid SA stations after filtering.");
            return;
        }

        db.Stations.AddRange(stations);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} SA stations from SA Fuel Pricing Information API.", stations.Count);
    }

    // ── Lookups ────────────────────────────────────────────────────────────────

    private async Task<(Dictionary<int, string> Brands, Dictionary<int, string> Regions)>
        FetchLookupsAsync(HttpClient client, string token, CancellationToken ct)
    {
        var brandsTask  = FetchBrandsAsync(client, token, ct);
        var regionsTask = FetchRegionsAsync(client, token, ct);
        await Task.WhenAll(brandsTask, regionsTask);
        return (await brandsTask, await regionsTask);
    }

    private async Task<Dictionary<int, string>> FetchBrandsAsync(HttpClient client, string token, CancellationToken ct)
    {
        try
        {
            var resp = await SendAsync(client, token,
                $"{ApiBase}/Subscriber/GetCountryBrands?countryId={CountryId}", ct);
            if (!resp.IsSuccessStatusCode) return [];
            var body   = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<GetCountryBrandsResponse>(body, JsonOpts);
            return result?.Brands?.ToDictionary(b => b.BrandId, b => b.Name) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch SA brand list — brand names will be 'Independent'.");
            return [];
        }
    }

    private async Task<Dictionary<int, string>> FetchRegionsAsync(HttpClient client, string token, CancellationToken ct)
    {
        try
        {
            var resp = await SendAsync(client, token,
                $"{ApiBase}/Subscriber/GetCountryGeographicRegions?countryId={CountryId}", ct);
            if (!resp.IsSuccessStatusCode) return [];
            var body   = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<GetCountryGeographicRegionsResponse>(body, JsonOpts);
            // GeoRegionLevel 1 = suburb-level entries; keyed by GeoRegionId → GeoRegionName
            return result?.GeographicRegions?
                .Where(r => r.GeoRegionLevel == 1)
                .ToDictionary(r => r.GeoRegionId, r => r.GeoRegionName) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch SA geographic regions — suburb names will be empty.");
            return [];
        }
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Authorization", $"FPDAPI SubscriberToken={token}");
        return client.SendAsync(req, ct);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class GetFullSiteDetailsResponse
    {
        public List<FppSiteDto> S { get; set; } = [];
    }

    private sealed class FppSiteDto
    {
        public long   S   { get; set; }   // Station ID
        public string N   { get; set; } = string.Empty;   // Name
        public string A   { get; set; } = string.Empty;   // Address
        public int    B   { get; set; }   // Brand ID
        public string P   { get; set; } = string.Empty;   // Postcode
        public int    G1  { get; set; }   // Suburb-level geographic region ID
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    private sealed class GetCountryBrandsResponse
    {
        public List<FppBrandDto> Brands { get; set; } = [];
    }

    private sealed class FppBrandDto
    {
        public int    BrandId { get; set; }
        public string Name    { get; set; } = string.Empty;
    }

    private sealed class GetCountryGeographicRegionsResponse
    {
        public List<FppGeoRegionDto> GeographicRegions { get; set; } = [];
    }

    private sealed class FppGeoRegionDto
    {
        public int    GeoRegionId    { get; set; }
        public int    GeoRegionLevel { get; set; }
        public string GeoRegionName  { get; set; } = string.Empty;
    }
}
