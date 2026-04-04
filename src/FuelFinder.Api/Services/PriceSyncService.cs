using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

public interface IPriceSyncService
{
    Task SyncAsync(CancellationToken ct = default);
}

/// <summary>
/// Fetches live fuel prices from four state government APIs and upserts them into StationPrices.
/// Each state sync is independent — a failure in one does not affect others.
/// Uses delete-all + bulk-insert per state to keep prices current without row-by-row upserts.
///
/// Matching strategy:
///   QLD/SA: fetch GetFullSiteDetails (SiteId → lat/lng) then match DB stations by lat/lng.
///           This works regardless of whether Station.ExternalId has been populated.
///   WA:     lat/lng from RSS items matched to DB stations directly.
///   NSW:    stationcode from v2/prices/full response; ExternalId match first,
///           then lat/lng fallback via the stations array embedded in the same response.
/// </summary>
public class PriceSyncService(
    IDbContextFactory<AppDbContext> dbFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<PriceSyncService> logger) : IPriceSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // FPP API FuelId → our FuelType
    private static readonly Dictionary<int, string> FppFuelMap = new()
    {
        [2]  = "ULP",
        [3]  = "Diesel",
        [5]  = "Premium",
        [6]  = "Diesel",
        [8]  = "Premium",
        [12] = "E10",
    };

    // WA FuelWatch product ID → our FuelType
    private static readonly Dictionary<int, string> WaProductMap = new()
    {
        [1]  = "ULP",
        [4]  = "Diesel",
        [5]  = "Premium",
        [11] = "E10",
    };

    // NSW FuelCheck fueltype string → our FuelType
    private static readonly Dictionary<string, string> NswFuelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["U91"] = "ULP",
        ["DL"]  = "Diesel",
        ["E10"] = "E10",
        ["P95"] = "Premium",
        ["P98"] = "Premium",
        ["PDL"] = "Diesel",
    };

    public async Task SyncAsync(CancellationToken ct = default)
    {
        // Each state gets its own DbContext — concurrent access to a single DbContext is unsafe.
        await Task.WhenAll(
            SyncQldAsync(ct),
            SyncSaAsync(ct),
            SyncWaAsync(ct),
            SyncNswAsync(ct));
    }

    // ── QLD ───────────────────────────────────────────────────────────────────
    // Matches via lat/lng (fetches GetFullSiteDetails for SiteId→lat/lng mapping).
    // This works for stations seeded before ExternalId was added.

    private async Task SyncQldAsync(CancellationToken ct)
    {
        try
        {
            var token = config["QldFuelPrices:SubscriberToken"];
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("QldFuelPrices:SubscriberToken not configured — skipping QLD price sync.");
                return;
            }

            var client = httpClientFactory.CreateClient("FuelPricesQld");

            // 1. Fetch prices
            var prices = await FetchFppPricesAsync(
                client, token,
                "https://fppdirectapi-prod.fuelpricesqld.com.au/Price/GetSitesPrices?countryId=21&geoRegionLevel=3&geoRegionId=1",
                "QLD", ct);
            if (prices is null) return;

            // 2. Fetch site details to get SiteId → lat/lng
            var siteLatLng = await FetchFppSiteLatLngAsync(
                client, token,
                "https://fppdirectapi-prod.fuelpricesqld.com.au/Subscriber/GetFullSiteDetails?countryId=21&geoRegionLevel=3&geoRegionId=1",
                "QLD", ct);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var dbStations = await db.Stations.Where(s => s.State == "QLD").AsNoTracking().ToListAsync(ct);
            // GroupBy to guard against any duplicate rounded lat/lng in the DB
            var byLatLng = dbStations
                .GroupBy(s => LatLngKey(s.Latitude, s.Longitude))
                .ToDictionary(g => g.Key, g => g.First());

            var now        = DateTimeOffset.UtcNow;
            var newPrices  = new List<StationPrice>();
            var seenKeys   = new HashSet<string>();
            var matched    = 0;

            foreach (var sp in prices)
            {
                if (!FppFuelMap.TryGetValue(sp.FuelId, out var fuelType)) continue;
                if (!siteLatLng.TryGetValue(sp.SiteId, out var llKey)) continue;
                if (!byLatLng.TryGetValue(llKey, out var station)) continue;

                var dedupKey = $"{station.Id}:{fuelType}";
                if (!seenKeys.Add(dedupKey)) continue;

                matched++;
                newPrices.Add(new StationPrice
                {
                    StationId          = station.Id,
                    FuelType           = fuelType,
                    PricePerLitreCents = sp.Price / 10m,
                    RecordedAtUtc      = sp.TransactionDateUtc != default
                        ? new DateTimeOffset(sp.TransactionDateUtc, TimeSpan.Zero)
                        : now,
                    Source             = "QLD",
                });
            }

            logger.LogInformation("QLD price sync: {Prices} prices → {Matched} matched DB stations ({Sites} sites from API, {DbStations} stations in DB).",
                prices.Count, matched, siteLatLng.Count, dbStations.Count);

            await PersistPricesAsync(db, "QLD", newPrices, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "QLD price sync failed.");
        }
    }

    // ── SA ────────────────────────────────────────────────────────────────────

    private async Task SyncSaAsync(CancellationToken ct)
    {
        try
        {
            var token = config["SaFuelPrices:SubscriberToken"];
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("SaFuelPrices:SubscriberToken not configured — skipping SA price sync.");
                return;
            }

            var client = httpClientFactory.CreateClient("SaFuelPrices");

            var prices = await FetchFppPricesAsync(
                client, token,
                "https://fppdirectapi-prod.safuelpricinginformation.com.au/Price/GetSitesPrices?countryId=21&geoRegionLevel=3&geoRegionId=4",
                "SA", ct);
            if (prices is null) return;

            var siteLatLng = await FetchFppSiteLatLngAsync(
                client, token,
                "https://fppdirectapi-prod.safuelpricinginformation.com.au/Subscriber/GetFullSiteDetails?countryId=21&geoRegionLevel=3&geoRegionId=4",
                "SA", ct);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var dbStations = await db.Stations.Where(s => s.State == "SA").AsNoTracking().ToListAsync(ct);
            var byLatLng = dbStations
                .GroupBy(s => LatLngKey(s.Latitude, s.Longitude))
                .ToDictionary(g => g.Key, g => g.First());

            var now       = DateTimeOffset.UtcNow;
            var newPrices = new List<StationPrice>();
            var seenKeys  = new HashSet<string>();
            var matched   = 0;

            foreach (var sp in prices)
            {
                if (!FppFuelMap.TryGetValue(sp.FuelId, out var fuelType)) continue;
                if (!siteLatLng.TryGetValue(sp.SiteId, out var llKey)) continue;
                if (!byLatLng.TryGetValue(llKey, out var station)) continue;

                var dedupKey = $"{station.Id}:{fuelType}";
                if (!seenKeys.Add(dedupKey)) continue;

                matched++;
                newPrices.Add(new StationPrice
                {
                    StationId          = station.Id,
                    FuelType           = fuelType,
                    PricePerLitreCents = sp.Price / 10m,
                    RecordedAtUtc      = sp.TransactionDateUtc != default
                        ? new DateTimeOffset(sp.TransactionDateUtc, TimeSpan.Zero)
                        : now,
                    Source             = "SA",
                });
            }

            logger.LogInformation("SA price sync: {Prices} prices → {Matched} matched DB stations ({Sites} sites from API, {DbStations} stations in DB).",
                prices.Count, matched, siteLatLng.Count, dbStations.Count);

            await PersistPricesAsync(db, "SA", newPrices, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SA price sync failed.");
        }
    }

    // ── WA ────────────────────────────────────────────────────────────────────

    private async Task SyncWaAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("FuelWatch");

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var waStations = await db.Stations.Where(s => s.State == "WA").AsNoTracking().ToListAsync(ct);
            var byLatLng = waStations
                .GroupBy(s => LatLngKey(s.Latitude, s.Longitude))
                .ToDictionary(g => g.Key, g => g.First());

            var now       = DateTimeOffset.UtcNow;
            var prices    = new List<StationPrice>();
            var seenKeys  = new HashSet<string>();
            var matched   = 0;

            foreach (var (product, fuelType) in WaProductMap)
            {
                var response = await client.GetAsync(
                    $"https://www.fuelwatch.wa.gov.au/fuelwatch/fuelWatchRSS?Product={product}", ct);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("WA FuelWatch Product={Product} returned {Status}.", product, response.StatusCode);
                    continue;
                }

                var xml = await response.Content.ReadAsStringAsync(ct);
                List<WaItem> items;
                try   { items = ParseWaItems(xml); }
                catch (Exception ex) { logger.LogWarning(ex, "WA RSS parse failed for Product={Product}.", product); continue; }

                foreach (var item in items)
                {
                    var key = LatLngKey(item.Latitude, item.Longitude);
                    if (!byLatLng.TryGetValue(key, out var station)) continue;

                    var dedupKey = $"{station.Id}:{fuelType}";
                    if (!seenKeys.Add(dedupKey)) continue;

                    matched++;
                    prices.Add(new StationPrice
                    {
                        StationId          = station.Id,
                        FuelType           = fuelType,
                        PricePerLitreCents = item.Price,
                        RecordedAtUtc      = now,
                        Source             = "WA",
                    });
                }
            }

            logger.LogInformation("WA price sync: {Matched} prices matched ({DbStations} stations in DB).",
                matched, waStations.Count);

            await PersistPricesAsync(db, "WA", prices, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WA price sync failed.");
        }
    }

    // ── NSW ───────────────────────────────────────────────────────────────────
    // Primary match: ExternalId (stationCode, set by seeder for newly seeded stations).
    // Fallback: lat/lng from the stations array embedded in the v2/prices/full response.
    // The fallback handles stations seeded before ExternalId was introduced.

    private async Task SyncNswAsync(CancellationToken ct)
    {
        try
        {
            var apiKey = config["FuelCheck:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogWarning("FuelCheck:ApiKey not configured — skipping NSW price sync.");
                return;
            }

            var client = httpClientFactory.CreateClient("FuelCheck");
            using var request = new HttpRequestMessage(
                HttpMethod.Get, "https://api.onegov.nsw.gov.au/FuelCheckApp/v2/fuel/prices/full");
            request.Headers.Add("apikey", apiKey);
            request.Headers.Add("requesttimestamp", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            request.Headers.IfModifiedSince = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("NSW FuelCheck v2 prices returned {Status}.", response.StatusCode);
                return;
            }

            var body   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<NswPricesResponse>(body, JsonOptions);
            if (result?.Prices is null or { Count: 0 })
            {
                logger.LogWarning("NSW FuelCheck returned no prices.");
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var nswStations = await db.Stations.Where(s => s.State == "NSW").AsNoTracking().ToListAsync(ct);

            // Primary: ExternalId (stationCode) → Station
            var byExtId = nswStations
                .Where(s => !string.IsNullOrEmpty(s.ExternalId))
                .ToDictionary(s => s.ExternalId!);

            // Fallback: lat/lng → Station (handles stations without ExternalId)
            var byLatLng = nswStations
                .GroupBy(s => LatLngKey(s.Latitude, s.Longitude))
                .ToDictionary(g => g.Key, g => g.First());

            // Build stationcode → LatLngKey from the response's stations list
            var responseStationLatLng = result.Stations?
                .Where(s => !string.IsNullOrEmpty(s.StationCode)
                         && (s.Latitude != 0 || s.Longitude != 0))
                .ToDictionary(s => s.StationCode!, s => LatLngKey(s.Latitude, s.Longitude))
                ?? [];

            logger.LogInformation("NSW price sync: {PriceCount} prices in response, {StationCount} stations in response, {DbStations} stations in DB ({WithExtId} with ExternalId, {LatLngEntries} lat/lng entries).",
                result.Prices.Count, responseStationLatLng.Count, nswStations.Count, byExtId.Count, byLatLng.Count);

            var now       = DateTimeOffset.UtcNow;
            var prices    = new List<StationPrice>();
            var seenKeys  = new HashSet<string>();
            var matched   = 0;
            var unmatched = 0;

            foreach (var p in result.Prices)
            {
                if (!NswFuelMap.TryGetValue(p.FuelType, out var fuelType)) continue;

                Models.Station? station = null;

                // 1. ExternalId match (fast path for newly seeded stations)
                if (byExtId.TryGetValue(p.StationCode, out var s1))
                    station = s1;
                // 2. lat/lng fallback via the stations array in the response
                else if (responseStationLatLng.TryGetValue(p.StationCode, out var llKey)
                      && byLatLng.TryGetValue(llKey, out var s2))
                    station = s2;

                if (station is null) { unmatched++; continue; }

                var dedupKey = $"{station.Id}:{fuelType}";
                if (!seenKeys.Add(dedupKey)) continue;

                matched++;
                var recordedAt = DateTime.TryParseExact(
                    p.LastUpdated, "dd/MM/yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt)
                    ? new DateTimeOffset(dt, TimeSpan.Zero)
                    : now;

                prices.Add(new StationPrice
                {
                    StationId          = station.Id,
                    FuelType           = fuelType,
                    PricePerLitreCents = (decimal)p.Price,
                    RecordedAtUtc      = recordedAt,
                    Source             = "NSW",
                });
            }

            logger.LogInformation("NSW price sync: {Matched} prices inserted, {Unmatched} unmatched (stations not in DB).",
                matched, unmatched);

            await PersistPricesAsync(db, "NSW", prices, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NSW price sync failed.");
        }
    }

    // ── FPP shared helpers ────────────────────────────────────────────────────

    private async Task<List<FppSitePrice>?> FetchFppPricesAsync(
        HttpClient client, string token, string url, string state, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Headers = { { "Authorization", $"FPDAPI SubscriberToken={token}" } }
        };
        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("{State} price API returned {Status}.", state, response.StatusCode);
            return null;
        }
        var body   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<FppPricesResponse>(body, JsonOptions);
        if (result?.SitePrices is null or { Count: 0 })
        {
            logger.LogWarning("{State} price API returned no prices.", state);
            return null;
        }
        return result.SitePrices;
    }

    /// <summary>Fetches GetFullSiteDetails and returns SiteId → LatLngKey.</summary>
    private async Task<Dictionary<long, string>> FetchFppSiteLatLngAsync(
        HttpClient client, string token, string url, string state, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Headers = { { "Authorization", $"FPDAPI SubscriberToken={token}" } }
        };
        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("{State} GetFullSiteDetails returned {Status}.", state, response.StatusCode);
            return [];
        }
        var body   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<FppSitesResponse>(body, JsonOptions);
        return result?.S?
            .Where(s => s.Lat != 0 || s.Lng != 0)
            .GroupBy(s => s.SiteId)
            .ToDictionary(g => (long)g.Key, g => LatLngKey(g.First().Lat, g.First().Lng))
            ?? [];
    }

    // ── General helpers ───────────────────────────────────────────────────────

    private static async Task PersistPricesAsync(
        AppDbContext db, string source, List<StationPrice> prices, CancellationToken ct)
    {
        using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.StationPrices.Where(p => p.Source == source).ExecuteDeleteAsync(ct);

        const int batchSize = 500;
        for (int i = 0; i < prices.Count; i += batchSize)
        {
            db.StationPrices.AddRange(prices.GetRange(i, Math.Min(batchSize, prices.Count - i)));
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        await tx.CommitAsync(ct);
    }

    private static string LatLngKey(double lat, double lng) =>
        $"{Math.Round(lat, 4)}|{Math.Round(lng, 4)}";

    private static List<WaItem> ParseWaItems(string xml)
    {
        var doc   = XDocument.Parse(xml);
        var items = new List<WaItem>();
        foreach (var el in doc.Descendants("item"))
        {
            if (!double.TryParse(el.Element("latitude")?.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat)) continue;
            if (!double.TryParse(el.Element("longitude")?.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lng)) continue;
            if (!decimal.TryParse(el.Element("price")?.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var price)) continue;
            items.Add(new WaItem(lat, lng, price));
        }
        return items;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class FppPricesResponse
    {
        public List<FppSitePrice>? SitePrices { get; set; }
    }

    private sealed class FppSitePrice
    {
        public long     SiteId             { get; set; }
        public int      FuelId             { get; set; }
        public decimal  Price              { get; set; }
        public DateTime TransactionDateUtc { get; set; }
    }

    // Re-uses the same site response shape as the seeders
    private sealed class FppSitesResponse
    {
        public List<FppSite>? S { get; set; }
    }

    private sealed class FppSite
    {
        [JsonPropertyName("S")] public int    SiteId { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    private sealed class NswPricesResponse
    {
        public List<NswStation>? Stations { get; set; }
        public List<NswPrice>?   Prices   { get; set; }
    }

    private sealed class NswStation
    {
        public string StationCode { get; set; } = string.Empty;
        public double Latitude    { get; set; }
        public double Longitude   { get; set; }
    }

    private sealed class NswPrice
    {
        public string StationCode { get; set; } = string.Empty;
        public string FuelType    { get; set; } = string.Empty;
        public double Price       { get; set; }
        public string LastUpdated { get; set; } = string.Empty;
    }

    private sealed record WaItem(double Latitude, double Longitude, decimal Price);
}
