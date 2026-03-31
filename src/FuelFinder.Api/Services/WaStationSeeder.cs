using System.Xml.Linq;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

/// <summary>
/// One-time seeder that populates WA stations from the WA FuelWatch public RSS feed.
/// No authentication required. Runs only when no WA stations exist in the database,
/// so it is safe to deploy alongside existing NSW data.
/// </summary>
public class WaStationSeeder(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<WaStationSeeder> logger)
{
    // Querying without a StateRegion returns all WA stations for that product.
    // We query ULP (1) and Diesel (4) to maximise coverage — some stations only sell one.
    private static readonly int[] Products = [1, 4];
    private const string FeedBase = "https://www.fuelwatch.wa.gov.au/fuelwatch/fuelWatchRSS";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Stations.AnyAsync(s => s.State == "WA", ct))
        {
            logger.LogInformation("WA stations already present — skipping WA seed.");
            return;
        }

        logger.LogInformation("Seeding WA stations from FuelWatch RSS…");

        var client   = httpClientFactory.CreateClient("FuelWatch");
        var seen     = new HashSet<string>();   // de-dupe by rounded lat|lng
        var stations = new List<Station>();

        foreach (var product in Products)
        {
            List<FuelWatchItem> items;
            try
            {
                var response = await client.GetAsync($"{FeedBase}?Product={product}", ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("FuelWatch Product={Product} returned {Status}.", product, response.StatusCode);
                    continue;
                }
                var xml = await response.Content.ReadAsStringAsync(ct);
                items = ParseItems(xml);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FuelWatch Product={Product} request failed.", product);
                continue;
            }

            foreach (var item in items)
            {
                if (item.Latitude == 0 && item.Longitude == 0) continue;
                if (string.IsNullOrWhiteSpace(item.TradingName)) continue;

                // Round to ~11 m precision for de-duplication
                var key = $"{Math.Round(item.Latitude, 4)}|{Math.Round(item.Longitude, 4)}";
                if (!seen.Add(key)) continue;

                stations.Add(new Station
                {
                    Id        = Guid.NewGuid(),
                    Name      = item.TradingName.Trim(),
                    Brand     = item.Brand.Trim(),
                    Address   = item.Address.Trim(),
                    Suburb    = ToTitleCase(item.Location.Trim()),
                    State     = "WA",
                    Latitude  = item.Latitude,
                    Longitude = item.Longitude,
                });
            }
        }

        if (stations.Count == 0)
        {
            logger.LogWarning("FuelWatch returned no usable stations.");
            return;
        }

        db.Stations.AddRange(stations);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} WA stations from FuelWatch.", stations.Count);
    }

    // ── XML parser ────────────────────────────────────────────────────────────

    private static List<FuelWatchItem> ParseItems(string xml)
    {
        var doc   = XDocument.Parse(xml);
        var items = new List<FuelWatchItem>();

        foreach (var el in doc.Descendants("item"))
        {
            if (!double.TryParse(el.Element("latitude")?.Value,  System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat)) continue;
            if (!double.TryParse(el.Element("longitude")?.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lng)) continue;

            items.Add(new FuelWatchItem(
                TradingName: el.Element("trading-name")?.Value ?? "",
                Brand:       el.Element("brand")?.Value        ?? "",
                Address:     el.Element("address")?.Value      ?? "",
                Location:    el.Element("location")?.Value     ?? "",
                Latitude:    lat,
                Longitude:   lng
            ));
        }

        return items;
    }

    // FuelWatch returns suburbs in ALL CAPS ("PERTH") — convert to title case
    private static string ToTitleCase(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    private sealed record FuelWatchItem(
        string TradingName,
        string Brand,
        string Address,
        string Location,
        double Latitude,
        double Longitude
    );
}
