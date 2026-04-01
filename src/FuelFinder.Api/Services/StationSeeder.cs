using System.Text.Json;
using FuelFinder.Api.Data;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelFinder.Api.Services;

/// <summary>
/// Pulls all NSW stations from the FuelCheck API on first boot (when Stations table is empty)
/// and upserts them into the database. No-op on subsequent boots.
/// </summary>
public class StationSeeder(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<StationSeeder> logger)
{
    private const string ApiBase = "https://api.onegov.nsw.gov.au/FuelCheckApp/v1/fuel";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var apiKey = config["FuelCheck:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("FuelCheck:ApiKey not configured — skipping station seed.");
            return;
        }

        if (await db.Stations.AnyAsync(s => s.State == "NSW", ct))
        {
            logger.LogInformation("NSW stations already present — skipping NSW seed.");
            return;
        }

        logger.LogInformation("Seeding stations from NSW FuelCheck API…");

        var client = httpClientFactory.CreateClient("FuelCheck");
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/lovs");
        request.Headers.Add("apikey", apiKey);
        request.Headers.Add("requesttimestamp", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        // if-modified-since must be RFC 7231 format for HttpHeaders to accept it
        request.Headers.IfModifiedSince = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var lovs = JsonSerializer.Deserialize<LovsResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (lovs?.Stations?.Items is null or { Count: 0 })
        {
            logger.LogWarning("FuelCheck /lovs returned no stations.");
            return;
        }

        var stations = lovs.Stations.Items
            .Where(s => s.Location is { Latitude: not 0, Longitude: not 0 }
                     && !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new Station
            {
                Id       = Guid.NewGuid(),
                Name     = s.Name.Trim(),
                Brand    = s.Brand.Trim(),
                Address  = ParseAddress(s.Address),
                Suburb   = ParseSuburb(s.Address),
                State    = "NSW",
                Latitude = s.Location!.Latitude,
                Longitude = s.Location.Longitude,
            })
            .ToList();

        db.Stations.AddRange(stations);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} stations from NSW FuelCheck.", stations.Count);
    }

    // Address format: "208-212 Pacific Hwy North, Coffs Harbour NSW 2450"
    private static string ParseAddress(string raw)
    {
        var commaIdx = raw.IndexOf(',');
        return commaIdx > 0 ? raw[..commaIdx].Trim() : raw.Trim();
    }

    private static string ParseSuburb(string raw)
    {
        var commaIdx = raw.IndexOf(',');
        if (commaIdx < 0) return string.Empty;

        // After comma: " Coffs Harbour NSW 2450" — drop postcode + state code
        var rest = raw[(commaIdx + 1)..].Trim();
        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Drop last token (postcode) and second-last (NSW)
        var suburb = parts.Length > 2
            ? string.Join(' ', parts[..^2])
            : parts.FirstOrDefault() ?? string.Empty;

        return suburb.Trim();
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class LovsResponse
    {
        public StationList? Stations { get; set; }
    }

    private sealed class StationList
    {
        public List<FuelCheckStation>? Items { get; set; }
    }

    private sealed class FuelCheckStation
    {
        public string Name    { get; set; } = string.Empty;
        public string Brand   { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public GeoLocation? Location { get; set; }
    }

    private sealed class GeoLocation
    {
        public double Latitude  { get; set; }
        public double Longitude { get; set; }
    }
}
