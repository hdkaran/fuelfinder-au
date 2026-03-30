using FuelFinder.Api.Cache;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace FuelFinder.Api.Services;

sealed class StationQueryService(AppDbContext db, IDistributedCache cache)
{
    private static readonly TimeSpan NearbyTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StationTtl = TimeSpan.FromMinutes(2);
    private const int RecentWindowHours = 4;
    private static readonly string[] AllFuelTypes = ["Diesel", "ULP", "E10", "Premium"];

    public async Task<IReadOnlyList<StationDto>> GetNearbyAsync(
        double lat, double lng, double radiusMetres, string? fuelType, CancellationToken ct)
    {
        var cacheKey = $"stations:nearby:{lat:F3}:{lng:F3}:{radiusMetres}:{fuelType ?? "all"}";
        var cached = await cache.GetJsonAsync<IReadOnlyList<StationDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var result = await QueryNearbyFromDbAsync(lat, lng, radiusMetres, fuelType, ct);
        await cache.SetJsonAsync(cacheKey, result, NearbyTtl, ct);
        return result;
    }

    public async Task<StationDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cacheKey = $"station:{id}";
        var cached = await cache.GetJsonAsync<StationDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var cutoff = DateTimeOffset.UtcNow.AddHours(-RecentWindowHours);
        var station = await db.Stations
            .Where(s => s.Id == id)
            .Include(s => s.Reports.Where(r => r.CreatedAt >= cutoff))
                .ThenInclude(r => r.FuelTypes)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (station is null) return null;

        var reports = station.Reports.OrderByDescending(r => r.CreatedAt).ToList();
        var dto = MapToDto(station, reports, distanceMetres: 0);
        await cache.SetJsonAsync(cacheKey, dto, StationTtl, ct);
        return dto;
    }

    public async Task<IReadOnlyList<StationDto>> SearchAsync(
        string q, double? lat, double? lng, CancellationToken ct)
    {
        var term = q.Trim();
        var cacheKey = $"stations:search:{term.ToLowerInvariant()}:{lat:F3}:{lng:F3}";
        var cached = await cache.GetJsonAsync<IReadOnlyList<StationDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var cutoff = DateTimeOffset.UtcNow.AddHours(-RecentWindowHours);
        var pattern = $"%{term}%";

        var stations = await db.Stations
            .Where(s => EF.Functions.Like(s.Name,    pattern)
                     || EF.Functions.Like(s.Brand,   pattern)
                     || EF.Functions.Like(s.Suburb,  pattern)
                     || EF.Functions.Like(s.Address, pattern))
            .Include(s => s.Reports.Where(r => r.CreatedAt >= cutoff))
                .ThenInclude(r => r.FuelTypes)
            .AsNoTracking()
            .Take(50)
            .ToListAsync(ct);

        var results = stations
            .Select(s =>
            {
                var dist = (lat.HasValue && lng.HasValue)
                    ? Haversine(lat.Value, lng.Value, s.Latitude, s.Longitude)
                    : 0;
                var reports = s.Reports.OrderByDescending(r => r.CreatedAt).ToList();
                return MapToDto(s, reports, dist);
            })
            .OrderBy(s => lat.HasValue ? s.DistanceMetres : 0)
            .ThenBy(s => s.Name)
            .Take(20)
            .ToList();

        await cache.SetJsonAsync(cacheKey, results, TimeSpan.FromMinutes(1), ct);
        return results;
    }

    public async Task InvalidateAsync(Guid stationId, CancellationToken ct)
    {
        await cache.RemoveSafeAsync($"station:{stationId}", ct);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<StationDto>> QueryNearbyFromDbAsync(
        double lat, double lng, double radiusMetres, string? fuelType, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-RecentWindowHours);

        // Rough bounding-box pre-filter in SQL (avoids full table scan)
        var latDelta = radiusMetres / 111_000.0;
        var lngDelta = radiusMetres / (111_000.0 * Math.Cos(lat * Math.PI / 180.0));

        var stations = await db.Stations
            .Where(s => s.Latitude  >= lat - latDelta && s.Latitude  <= lat + latDelta
                     && s.Longitude >= lng - lngDelta && s.Longitude <= lng + lngDelta)
            .Include(s => s.Reports.Where(r => r.CreatedAt >= cutoff))
                .ThenInclude(r => r.FuelTypes)
            .AsNoTracking()
            .ToListAsync(ct);

        var results = new List<StationDto>(stations.Count);
        foreach (var station in stations)
        {
            var dist = Haversine(lat, lng, station.Latitude, station.Longitude);
            if (dist > radiusMetres) continue;

            var reports = station.Reports.OrderByDescending(r => r.CreatedAt).ToList();

            // fuelType filter: only include stations with a recent report confirming availability
            if (fuelType is not null)
            {
                var hasAvailableFuel = reports.Any(r =>
                    r.FuelTypes.Any(ft => ft.FuelType == fuelType && ft.Available));
                if (!hasAvailableFuel) continue;
            }

            results.Add(MapToDto(station, reports, dist));
        }

        results.Sort((a, b) => a.DistanceMetres.CompareTo(b.DistanceMetres));
        return results;
    }

    private static StationDto MapToDto(Station station, IReadOnlyList<Report> reports, double distanceMetres)
    {
        var latest = reports.FirstOrDefault(); // sorted newest-first by caller
        var status = latest?.Status ?? "unknown";

        // For each fuel type, find the most recent report that mentions it
        var fuelAvailability = AllFuelTypes.Select(ft =>
        {
            bool? available = null;
            foreach (var report in reports)
            {
                var mention = report.FuelTypes.FirstOrDefault(rft => rft.FuelType == ft);
                if (mention is not null) { available = mention.Available; break; }
            }
            return new FuelAvailabilityDto(ft, available);
        }).ToList();

        var lastMinutesAgo = latest is null
            ? (int?)null
            : (int)(DateTimeOffset.UtcNow - latest.CreatedAt).TotalMinutes;

        return new StationDto(
            station.Id, station.Name, station.Brand, station.Address,
            station.Suburb, station.State, station.Latitude, station.Longitude,
            distanceMetres, status, fuelAvailability, reports.Count, lastMinutesAgo);
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
