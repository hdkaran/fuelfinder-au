using FuelFinder.Api.Cache;
using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace FuelFinder.Api.Services;

sealed class StatsService(AppDbContext db, IDistributedCache cache)
{
    private static readonly TimeSpan StatsTtl = TimeSpan.FromMinutes(1);
    private const string CacheKey = "stats:summary";

    public async Task<StatsDto> GetSummaryAsync(CancellationToken ct)
    {
        var cached = await cache.GetJsonAsync<StatsDto>(CacheKey, ct);
        if (cached is not null) return cached;

        // Use DateTimeOffset (not DateTime) — EF Core 10 is strict about comparing
        // DateTimeOffset columns against DateTime values in LINQ queries.
        var todayUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        var totalReportsToday = await db.Reports
            .CountAsync(r => r.CreatedAt >= todayUtc, ct);

        var stationsAffected = await db.Reports
            .Where(r => r.CreatedAt >= todayUtc)
            .Select(r => r.StationId)
            .Distinct()
            .CountAsync(ct);

        var dto = new StatsDto(
            totalReportsToday,
            stationsAffected,
            DateTimeOffset.UtcNow.ToString("O"));

        await cache.SetJsonAsync(CacheKey, dto, StatsTtl, ct);
        return dto;
    }

    public async Task<IReadOnlyList<TodayReportDto>> GetTodayReportsAsync(CancellationToken ct)
    {
        var todayUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;

        var reports = await db.Reports
            .Where(r => r.CreatedAt >= todayUtc)
            .Include(r => r.Station)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return reports.Select(r => new TodayReportDto(
            r.Id,
            r.StationId,
            r.Station.Name,
            r.Station.Address,
            r.Status,
            (int)(now - r.CreatedAt).TotalMinutes
        )).ToList();
    }

    public async Task<IReadOnlyList<AffectedStationDto>> GetAffectedStationsAsync(CancellationToken ct)
    {
        var todayUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

        var reports = await db.Reports
            .Where(r => r.CreatedAt >= todayUtc)
            .Include(r => r.Station)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return reports
            .GroupBy(r => r.StationId)
            .Select(g =>
            {
                var station = g.First().Station;
                return new AffectedStationDto(
                    station.Id,
                    station.Name,
                    station.Address,
                    station.Suburb,
                    station.State,
                    g.First().Status,
                    g.Count()
                );
            })
            .ToList();
    }
}
