using System.Text.Json;
using System.Text.Json.Serialization;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

/// <summary>
/// One-time seeder that populates QLD stations from the Queensland Fuel Price Reporting API.
/// Requires a subscriber token in QldFuelPrices:SubscriberToken (injected from Key Vault in prod).
/// Uses geoRegionLevel=3 / geoRegionId=1 which maps to the Queensland state region.
/// Suburb is resolved from the G1 (level-1 geographic region) field on each site.
/// </summary>
public class QldStationSeeder(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<QldStationSeeder> logger)
{
    private const string ApiBase   = "https://fppdirectapi-prod.fuelpricesqld.com.au";
    private const int    CountryId = 21; // Australia

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var token = config["QldFuelPrices:SubscriberToken"];
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("QldFuelPrices:SubscriberToken not configured — skipping QLD station seed.");
            return;
        }

        if (await db.Stations.AnyAsync(s => s.State == "QLD", ct))
        {
            logger.LogInformation("QLD stations already present — skipping QLD seed.");
            return;
        }

        logger.LogInformation("Seeding QLD stations from FuelPricesQLD API…");

        var client = httpClientFactory.CreateClient("FuelPricesQld");

        // Fetch brand lookup: BrandId → Name
        var brands = await FetchBrandsAsync(client, token, ct);
        if (brands.Count == 0)
            logger.LogWarning("FuelPricesQLD returned no brands — brand names will be empty.");

        // Fetch suburb lookup: G1 region ID → suburb name (level-1 geographic regions)
        var suburbs = await FetchSuburbLookupAsync(client, token, ct);
        if (suburbs.Count == 0)
            logger.LogWarning("FuelPricesQLD returned no geographic regions — suburb names will be empty.");

        // geoRegionLevel=3 / geoRegionId=1 = Queensland state region
        List<QldSite> sites;
        try
        {
            sites = await FetchSitesAsync(client, token, ct);
            logger.LogInformation("FuelPricesQLD returned {Count} QLD sites.", sites.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FuelPricesQLD GetFullSiteDetails failed.");
            return;
        }

        var seen     = new HashSet<int>();
        var stations = new List<Station>();

        foreach (var site in sites)
        {
            if (!seen.Add(site.SiteId)) continue;
            if (site.Lat == 0 && site.Lng == 0) continue;
            if (string.IsNullOrWhiteSpace(site.Name)) continue;

            brands.TryGetValue(site.BrandId, out var brandName);
            suburbs.TryGetValue(site.G1, out var suburb);

            stations.Add(new Station
            {
                Id        = Guid.NewGuid(),
                Name      = site.Name.Trim(),
                Brand     = brandName?.Trim() ?? string.Empty,
                Address   = site.Address.Trim(),
                Suburb    = ToTitleCase(suburb ?? string.Empty),
                State     = "QLD",
                Latitude  = site.Lat,
                Longitude = site.Lng,
            });
        }

        if (stations.Count == 0)
        {
            logger.LogWarning("FuelPricesQLD returned no usable QLD stations.");
            return;
        }

        db.Stations.AddRange(stations);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} QLD stations from FuelPricesQLD.", stations.Count);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Dictionary<int, string>> FetchBrandsAsync(HttpClient client, string token, CancellationToken ct)
    {
        using var request = BuildRequest($"/Subscriber/GetCountryBrands?countryId={CountryId}", token);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("GetCountryBrands returned {Status}.", response.StatusCode);
            return [];
        }

        var body   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<BrandsResponse>(body, JsonOptions);
        return result?.Brands?.ToDictionary(b => b.BrandId, b => b.Name) ?? [];
    }

    private async Task<Dictionary<long, string>> FetchSuburbLookupAsync(HttpClient client, string token, CancellationToken ct)
    {
        using var request = BuildRequest($"/Subscriber/GetCountryGeographicRegions?countryId={CountryId}", token);
        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("GetCountryGeographicRegions returned {Status}.", response.StatusCode);
            return [];
        }

        var body   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<RegionsResponse>(body, JsonOptions);

        // Level-1 regions are suburb/locality names; G1 on each site references these IDs
        return result?.GeographicRegions?
            .Where(r => r.GeoRegionLevel == 1)
            .ToDictionary(r => r.GeoRegionId, r => r.Name)
            ?? [];
    }

    private async Task<List<QldSite>> FetchSitesAsync(HttpClient client, string token, CancellationToken ct)
    {
        var path = $"/Subscriber/GetFullSiteDetails?countryId={CountryId}&geoRegionLevel=3&geoRegionId=1";
        using var request = BuildRequest(path, token);

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<SitesResponse>(body, JsonOptions);
        return result?.S ?? [];
    }

    private static HttpRequestMessage BuildRequest(string path, string token) =>
        new(HttpMethod.Get, $"{ApiBase}{path}")
        {
            Headers = { { "Authorization", $"FPDAPI SubscriberToken={token}" } }
        };

    // Suburb names come back in ALL CAPS ("SURAT") — convert to title case
    private static string ToTitleCase(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class BrandsResponse
    {
        public List<QldBrand>? Brands { get; set; }
    }

    private sealed class QldBrand
    {
        public int    BrandId { get; set; }
        public string Name    { get; set; } = string.Empty;
    }

    private sealed class RegionsResponse
    {
        public List<QldRegion>? GeographicRegions { get; set; }
    }

    private sealed class QldRegion
    {
        public int    GeoRegionLevel { get; set; }
        public long   GeoRegionId   { get; set; }
        public string Name          { get; set; } = string.Empty;
    }

    private sealed class SitesResponse
    {
        public List<QldSite>? S { get; set; }
    }

    private sealed class QldSite
    {
        [JsonPropertyName("S")]  public int    SiteId   { get; set; }
        [JsonPropertyName("A")]  public string Address  { get; set; } = string.Empty;
        [JsonPropertyName("N")]  public string Name     { get; set; } = string.Empty;
        [JsonPropertyName("B")]  public int    BrandId  { get; set; }
        [JsonPropertyName("P")]  public string Postcode { get; set; } = string.Empty;
        [JsonPropertyName("G1")] public long   G1       { get; set; }  // level-1 region = suburb
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
