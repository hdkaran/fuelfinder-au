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

        // Build brand lookup: BrandId → Name
        var brands = await FetchBrandsAsync(client, token, ct);
        if (brands.Count == 0)
            logger.LogWarning("FuelPricesQLD returned no brands — brand names will be empty.");

        // geoRegionLevel=3 / geoRegionId=1 is the Queensland state-level region
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

        var seen     = new HashSet<int>();  // de-dupe by SiteId
        var stations = new List<Station>();

        foreach (var site in sites)
        {
            if (!seen.Add(site.SiteId)) continue;
            if (site.Lat == 0 && site.Lng == 0) continue;
            if (string.IsNullOrWhiteSpace(site.Name)) continue;

            brands.TryGetValue(site.BrandId, out var brandName);

            stations.Add(new Station
            {
                Id        = Guid.NewGuid(),
                Name      = site.Name.Trim(),
                Brand     = brandName?.Trim() ?? string.Empty,
                Address   = ParseStreetAddress(site.Address),
                Suburb    = ParseSuburb(site.Address, site.Postcode),
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

    private async Task<List<QldSite>> FetchSitesAsync(HttpClient client, string token, CancellationToken ct)
    {
        // Level 3 = state region; GeoRegionId 1 = QUEENSLAND
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

    // Address examples: "61 Burrowes St", "126 Barwon Street" — suburb is in a separate postcode lookup
    // Since the API doesn't return suburb directly, extract from address or use postcode as fallback
    private static string ParseStreetAddress(string raw) => raw.Trim();

    private static string ParseSuburb(string address, string postcode)
    {
        // The QLD API address field is street only (e.g. "61 Burrowes St")
        // Suburb isn't in the address — return empty; postcode is available if needed
        return string.Empty;
    }

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

    private sealed class SitesResponse
    {
        // Response uses compressed single-letter key: { "S": [...] }
        public List<QldSite>? S { get; set; }
    }

    private sealed class QldSite
    {
        [JsonPropertyName("S")] public int    SiteId   { get; set; }
        [JsonPropertyName("A")] public string Address  { get; set; } = string.Empty;
        [JsonPropertyName("N")] public string Name     { get; set; } = string.Empty;
        [JsonPropertyName("B")] public int    BrandId  { get; set; }
        [JsonPropertyName("P")] public string Postcode { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
