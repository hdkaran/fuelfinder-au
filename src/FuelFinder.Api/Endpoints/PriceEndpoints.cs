using FuelFinder.Api.Cache;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace FuelFinder.Api.Endpoints;

static class PriceEndpoints
{
    internal static void MapPriceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/prices");

        // GET /api/prices/nearby?lat=&lng=&radius=&fuelType=
        group.MapGet("/nearby", async (
            double lat, double lng, double radius, string? fuelType,
            AppDbContext db, IDistributedCache cache, CancellationToken ct) =>
        {
            var cacheKey = $"prices:nearby:{lat:F2}:{lng:F2}:{radius}:{fuelType ?? "all"}";
            var cached   = await cache.GetJsonAsync<IReadOnlyList<PriceDto>>(cacheKey, ct);
            if (cached is not null) return Results.Ok(cached);

            var latDelta = radius / 111_000.0;
            var lngDelta = radius / (111_000.0 * Math.Cos(lat * Math.PI / 180.0));

            // Load stations in bounding box, then apply Haversine
            var stationsInBox = await db.Stations
                .Where(s => s.Latitude  >= lat - latDelta && s.Latitude  <= lat + latDelta
                         && s.Longitude >= lng - lngDelta && s.Longitude <= lng + lngDelta)
                .AsNoTracking()
                .ToListAsync(ct);

            var stationsInRadius = stationsInBox
                .Select(s => (Station: s, Dist: Haversine(lat, lng, s.Latitude, s.Longitude)))
                .Where(x => x.Dist <= radius)
                .ToList();

            if (stationsInRadius.Count == 0)
                return Results.Ok(Array.Empty<PriceDto>());

            var stationIds   = stationsInRadius.Select(x => x.Station.Id).ToList();
            var distanceLookup = stationsInRadius.ToDictionary(x => x.Station.Id, x => x);

            var pricesQuery = db.StationPrices
                .Where(p => stationIds.Contains(p.StationId));

            if (!string.IsNullOrEmpty(fuelType))
                pricesQuery = pricesQuery.Where(p => p.FuelType == fuelType);

            var prices = await pricesQuery.AsNoTracking().ToListAsync(ct);

            var staleCutoff = DateTimeOffset.UtcNow.AddHours(-2);
            var result = prices
                .Select(p =>
                {
                    var (station, dist) = distanceLookup[p.StationId];
                    return new PriceDto(
                        p.StationId, station.Name, station.Brand, station.Address, station.Suburb,
                        dist, p.FuelType, p.PricePerLitreCents, p.RecordedAtUtc,
                        p.RecordedAtUtc < staleCutoff);
                })
                .OrderBy(p => p.PricePerLitreCents)
                .ToList();

            await cache.SetJsonAsync(cacheKey, result, TimeSpan.FromMinutes(5), ct);
            return Results.Ok(result);
        });
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
