using System.Text.Json;
using System.Text.Json.Serialization;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

/// <summary>
/// One-time seeder that populates SA stations from the South Australia Fuel Pricing Information API.
/// Requires a subscriber token in SaFuelPrices:SubscriberToken (injected from Key Vault in prod).
/// Uses geoRegionLevel=3 / geoRegionId=4 which maps to the South Australia state region.
/// Suburb is resolved from the G1 (level-1 geographic region) field on each site.
/// </summary>
public class SaStationSeeder(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SaStationSeeder> logger)
{
    private const string ApiBase   = "https://fppdirectapi-prod.safuelpricinginformation.com.au";
    private const int    CountryId = 21; // Australia

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var token = config["SaFuelPrices:SubscriberToken"];
        if (string.IsNullOrEmpty(token))
        {
            logger.LogWarning("SaFuelPrices:SubscriberToken not configured — skipping SA station seed.");
            return;
        }

        if (await db.Stations.AnyAsync(s => s.State == "SA", ct))
        {
            logger.LogInformation("SA stations already present — skipping SA seed.");
            return;
        }

        logger.LogInformation("Seeding SA stations from SA Fuel Pricing API…");

        var client = httpClientFactory.CreateClient("SaFuelPrices");

        // Fetch brand lookup: BrandId → Name
        var brands = await FetchBrandsAsync(client, token, ct);
        if (brands.Count == 0)
            logger.LogWarning("SA Fuel Pricing API returned no brands — brand names will be empty.");

        // Fetch suburb lookup: G1 region ID → suburb name (level-1 geographic regions)
        var suburbs = await FetchSuburbLookupAsync(client, token, ct);
        if (suburbs.Count == 0)
            logger.LogWarning("SA Fuel Pricing API returned no geographic regions — suburb names will be empty.");

        // geoRegionLevel=3 / geoRegionId=4 = South Australia state region
        List<SaSite> sites;
        try
        {
            sites = await FetchSitesAsync(client, token, ct);
            logger.LogInformation("SA Fuel Pricing API returned {Count} SA sites.", sites.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SA Fuel Pricing API GetFullSiteDetails failed.");
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
                State     = "SA",
                Latitude  = site.Lat,
                Longitude = site.Lng,
            });
        }

        if (stations.Count == 0)
        {
            logger.LogWarning("SA Fuel Pricing API returned no usable SA stations.");
            return;
        }

        db.Stations.AddRange(stations);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} SA stations from SA Fuel Pricing API.", stations.Count);
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

    private async Task<List<SaSite>> FetchSitesAsync(HttpClient client, string token, CancellationToken ct)
    {
        // Level 3 = state region; GeoRegionId 4 = SOUTH AUSTRALIA
        var path = $"/Subscriber/GetFullSiteDetails?countryId={CountryId}&geoRegionLevel=3&geoRegionId=4";
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

    // Suburb names come back in ALL CAPS — convert to title case
    private static string ToTitleCase(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class BrandsResponse
    {
        public List<SaBrand>? Brands { get; set; }
    }

    private sealed class SaBrand
    {
        public int    BrandId { get; set; }
        public string Name    { get; set; } = string.Empty;
    }

    private sealed class RegionsResponse
    {
        public List<SaRegion>? GeographicRegions { get; set; }
    }

    private sealed class SaRegion
    {
        public int    GeoRegionLevel { get; set; }
        public long   GeoRegionId   { get; set; }
        public string Name          { get; set; } = string.Empty;
    }

    private sealed class SitesResponse
    {
        public List<SaSite>? S { get; set; }
    }

    private sealed class SaSite
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
