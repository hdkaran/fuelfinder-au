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
            const string url = "https://fppdirectapi-prod.fuelpricesqld.com.au/Price/GetSitesPrices?countryId=21&geoRegionLevel=3&geoRegionId=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { "Authorization", $"FPDAPI SubscriberToken={token}" } }
            };

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("QLD price API returned {Status}.", response.StatusCode);
                return;
            }

            var body   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<FppPricesResponse>(body, JsonOptions);
            if (result?.SitePrices is null or { Count: 0 })
            {
                logger.LogWarning("QLD price API returned no prices.");
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var stations = await db.Stations
                .Where(s => s.State == "QLD" && s.ExternalId != null)
                .AsNoTracking()
                .ToListAsync(ct);

            var byExtId = stations.ToDictionary(s => s.ExternalId!);
            var now     = DateTimeOffset.UtcNow;
            var prices  = new List<StationPrice>();

            foreach (var sp in result.SitePrices)
            {
                if (!FppFuelMap.TryGetValue(sp.FuelId, out var fuelType)) continue;
                if (!byExtId.TryGetValue(sp.SiteId.ToString(), out var station)) continue;

                prices.Add(new StationPrice
                {
                    StationId          = station.Id,
                    FuelType           = fuelType,
                    PricePerLitreCents = sp.Price / 10m, // tenths-of-cent → cents/litre
                    RecordedAtUtc      = sp.TransactionDateUtc != default
                        ? new DateTimeOffset(sp.TransactionDateUtc, TimeSpan.Zero)
                        : now,
                    Source             = "QLD",
                });
            }

            await PersistPricesAsync(db, "QLD", prices, ct);
            logger.LogInformation("QLD price sync: {Count} prices upserted.", prices.Count);
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
            const string url = "https://fppdirectapi-prod.safuelpricinginformation.com.au/Price/GetSitesPrices?countryId=21&geoRegionLevel=3&geoRegionId=4";
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { { "Authorization", $"FPDAPI SubscriberToken={token}" } }
            };

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("SA price API returned {Status}.", response.StatusCode);
                return;
            }

            var body   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<FppPricesResponse>(body, JsonOptions);
            if (result?.SitePrices is null or { Count: 0 })
            {
                logger.LogWarning("SA price API returned no prices.");
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var stations = await db.Stations
                .Where(s => s.State == "SA" && s.ExternalId != null)
                .AsNoTracking()
                .ToListAsync(ct);

            var byExtId = stations.ToDictionary(s => s.ExternalId!);
            var now     = DateTimeOffset.UtcNow;
            var prices  = new List<StationPrice>();

            foreach (var sp in result.SitePrices)
            {
                if (!FppFuelMap.TryGetValue(sp.FuelId, out var fuelType)) continue;
                if (!byExtId.TryGetValue(sp.SiteId.ToString(), out var station)) continue;

                prices.Add(new StationPrice
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

            await PersistPricesAsync(db, "SA", prices, ct);
            logger.LogInformation("SA price sync: {Count} prices upserted.", prices.Count);
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

            var waStations = await db.Stations
                .Where(s => s.State == "WA")
                .AsNoTracking()
                .ToListAsync(ct);

            var byLatLng = waStations.ToDictionary(s => LatLngKey(s.Latitude, s.Longitude));

            var now       = DateTimeOffset.UtcNow;
            var prices    = new List<StationPrice>();
            var seenKeys  = new HashSet<string>(); // deduplicate (stationId, fuelType)

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

                    prices.Add(new StationPrice
                    {
                        StationId          = station.Id,
                        FuelType           = fuelType,
                        PricePerLitreCents = item.Price, // already cents/litre
                        RecordedAtUtc      = now,
                        Source             = "WA",
                    });
                }
            }

            await PersistPricesAsync(db, "WA", prices, ct);
            logger.LogInformation("WA price sync: {Count} prices upserted.", prices.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WA price sync failed.");
        }
    }

    // ── NSW ───────────────────────────────────────────────────────────────────

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

            // Build stationcode → Station lookup
            // Try ExternalId first (set by NSW seeder), fall back to lat/lng from the response's stations list
            var nswStations = await db.Stations
                .Where(s => s.State == "NSW")
                .AsNoTracking()
                .ToListAsync(ct);

            var byExtId  = nswStations
                .Where(s => !string.IsNullOrEmpty(s.ExternalId))
                .ToDictionary(s => s.ExternalId!);

            var byLatLng = nswStations
                .ToDictionary(s => LatLngKey(s.Latitude, s.Longitude));

            // Build stationcode → lat/lng from the response's station list (for fallback matching)
            var responseLatLng = result.Stations?
                .Where(s => !string.IsNullOrEmpty(s.StationCode))
                .ToDictionary(s => s.StationCode!, s => LatLngKey(s.Latitude, s.Longitude))
                ?? [];

            var now      = DateTimeOffset.UtcNow;
            var prices   = new List<StationPrice>();
            var seenKeys = new HashSet<string>();

            foreach (var p in result.Prices)
            {
                if (!NswFuelMap.TryGetValue(p.FuelType, out var fuelType)) continue;

                // Resolve station: ExternalId (stationcode) → lat/lng fallback
                Models.Station? station = null;
                if (byExtId.TryGetValue(p.StationCode, out var s1))
                    station = s1;
                else if (responseLatLng.TryGetValue(p.StationCode, out var llKey)
                      && byLatLng.TryGetValue(llKey, out var s2))
                    station = s2;

                if (station is null) continue;

                var dedupKey = $"{station.Id}:{fuelType}";
                if (!seenKeys.Add(dedupKey)) continue;

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
                    PricePerLitreCents = (decimal)p.Price, // already cents/litre
                    RecordedAtUtc      = recordedAt,
                    Source             = "NSW",
                });
            }

            await PersistPricesAsync(db, "NSW", prices, ct);
            logger.LogInformation("NSW price sync: {Count} prices upserted.", prices.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NSW price sync failed.");
        }
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

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
        public long     SiteId               { get; set; }
        public int      FuelId               { get; set; }
        public decimal  Price                { get; set; }
        public DateTime TransactionDateUtc   { get; set; }
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
        public string  StationCode  { get; set; } = string.Empty;
        public string  FuelType     { get; set; } = string.Empty;
        public double  Price        { get; set; }
        public string  LastUpdated  { get; set; } = string.Empty;
    }

    private sealed record WaItem(double Latitude, double Longitude, decimal Price);
}
