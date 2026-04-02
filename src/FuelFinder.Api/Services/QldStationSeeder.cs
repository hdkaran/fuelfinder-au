using System.Text.Json;
using System.Text.Json.Serialization;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

/// <summary>
/// One-time seeder that populates QLD stations from the Queensland Fuel Price Reporting API.
/// Requires a subscriber token in QldFuelPrices:SubscriberToken (injected from Key Vault in prod).
/// Iterates all level-3 geographic regions to ensure full state coverage.
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

        // Discover all level-3 geographic regions (zones/districts) to get full QLD coverage
        var regionIds = await FetchRegionIdsAsync(client, token, ct);
        if (regionIds.Count == 0)
        {
            logger.LogWarning("Could not fetch QLD regions — falling back to region 1.");
            regionIds = [1];
        }

        var seen     = new HashSet<int>();  // de-dupe by SiteId
        var stations = new List<Station>();

        foreach (var regionId in regionIds)
        {
            List<QldSite> sites;
            try
            {
                sites = await FetchSitesAsync(client, token, regionId, ct);
                logger.LogInformation("Region {RegionId}: {Count} sites fetched.", regionId, sites.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FuelPricesQLD GetFullSiteDetails region {RegionId} failed — skipping.", regionId);
                continue;
            }

            foreach (var site in sites)
            {
                if (!seen.Add(site.SiteId)) continue;
                if (site.Geo is null || (site.Geo.Lat == 0 && site.Geo.Lng == 0)) continue;
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
                    Latitude  = site.Geo.Lat,
                    Longitude = site.Geo.Lng,
                });
            }
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

        return result?.Brands?.ToDictionary(b => b.BrandId, b => b.Name)
               ?? [];
    }

    private async Task<List<int>> FetchRegionIdsAsync(HttpClient client, string token, CancellationToken ct)
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

        // Level 3 = district/zone — collect all to ensure full state coverage
        return result?.Regions?
            .Where(r => r.GeoRegionLevel == 3)
            .Select(r => r.GeoRegionId)
            .Distinct()
            .ToList() ?? [];
    }

    private async Task<List<QldSite>> FetchSitesAsync(HttpClient client, string token, int regionId, CancellationToken ct)
    {
        var path = $"/Subscriber/GetFullSiteDetails?countryId={CountryId}&geoRegionLevel=3&geoRegionId={regionId}";
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

    // Address examples from the API:
    //   "38 Ingham Road, Garbutt QLD 4814"
    //   "123 Main St Woolloongabba QLD 4102"
    private static string ParseStreetAddress(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var commaIdx = raw.IndexOf(',');
        return commaIdx > 0 ? raw[..commaIdx].Trim() : raw.Trim();
    }

    private static string ParseSuburb(string raw, string postcode)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var clean = raw.Trim();

        // Strip postcode
        if (!string.IsNullOrWhiteSpace(postcode))
            clean = clean.Replace(postcode, "", StringComparison.Ordinal).Trim().TrimEnd(',').Trim();

        // Strip " QLD" state suffix
        if (clean.EndsWith(" QLD", StringComparison.OrdinalIgnoreCase))
            clean = clean[..^4].Trim();

        // Take the segment after the last comma as suburb
        var commaIdx = clean.LastIndexOf(',');
        return commaIdx >= 0 ? clean[(commaIdx + 1)..].Trim() : clean;
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

    private sealed class RegionsResponse
    {
        public List<QldRegion>? Regions { get; set; }
    }

    private sealed class QldRegion
    {
        public int    GeoRegionId    { get; set; }
        public int    GeoRegionLevel { get; set; }
        public string Name           { get; set; } = string.Empty;
    }

    private sealed class SitesResponse
    {
        // Response uses compressed single-letter keys: { "S": [...] }
        public List<QldSite>? S { get; set; }
    }

    private sealed class QldSite
    {
        [JsonPropertyName("S")] public int    SiteId      { get; set; }
        [JsonPropertyName("A")] public string Address     { get; set; } = string.Empty;
        [JsonPropertyName("N")] public string Name        { get; set; } = string.Empty;
        [JsonPropertyName("B")] public int    BrandId     { get; set; }
        [JsonPropertyName("P")] public string Postcode    { get; set; } = string.Empty;
        [JsonPropertyName("G")] public int    GeoRegionId { get; set; }
        public QldGeo? Geo { get; set; }
    }

    private sealed class QldGeo
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
