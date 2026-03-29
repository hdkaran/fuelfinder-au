using FuelFinder.Api.Cache;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace FuelFinder.Api.Services;

sealed class ReportService(AppDbContext db, IDistributedCache cache, StationQueryService stationCache)
{
    private static readonly TimeSpan RecentTtl = TimeSpan.FromSeconds(30);
    private const int RecentLimit = 10;

    /// <summary>
    /// Saves a new report. Returns the new report ID, or null if the station doesn't exist.
    /// </summary>
    public async Task<Guid?> SubmitAsync(ReportPayload payload, CancellationToken ct)
    {
        var stationExists = await db.Stations.AnyAsync(s => s.Id == payload.StationId, ct);
        if (!stationExists) return null;

        var report = new Report
        {
            Id = Guid.NewGuid(),
            StationId = payload.StationId,
            Status = payload.Status,
            Latitude = payload.Latitude,
            Longitude = payload.Longitude,
            CreatedAt = DateTimeOffset.UtcNow,
            FuelTypes = payload.FuelTypes
                .Select(ft => new ReportFuelType { FuelType = ft.FuelType, Available = ft.Available })
                .ToList()
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        // Invalidate caches affected by this new report
        await stationCache.InvalidateAsync(payload.StationId, ct);
        await cache.RemoveSafeAsync($"reports:recent:{payload.StationId}", ct);
        await cache.RemoveSafeAsync("stats:summary", ct);

        return report.Id;
    }

    /// <summary>
    /// Returns the most recent reports for a station (newest first).
    /// </summary>
    public async Task<IReadOnlyList<ReportDto>> GetRecentAsync(Guid stationId, CancellationToken ct)
    {
        var cacheKey = $"reports:recent:{stationId}";
        var cached = await cache.GetJsonAsync<IReadOnlyList<ReportDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var reports = await db.Reports
            .Where(r => r.StationId == stationId)
            .Include(r => r.FuelTypes)
            .OrderByDescending(r => r.CreatedAt)
            .Take(RecentLimit)
            .AsNoTracking()
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var dtos = reports.Select(r => new ReportDto(
            r.Id,
            r.Status,
            r.FuelTypes.Select(ft => new ReportFuelTypeDto(ft.FuelType, ft.Available)).ToList(),
            r.CreatedAt,
            (int)(now - r.CreatedAt).TotalMinutes
        )).ToList();

        await cache.SetJsonAsync(cacheKey, dtos, RecentTtl, ct);
        return dtos;
    }
}
