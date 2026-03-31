using FuelFinder.Api.Services;

namespace FuelFinder.Api.Endpoints;

static class StatsEndpoints
{
    internal static void MapStatsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/stats");

        // GET /api/stats/summary
        group.MapGet("/summary", async (StatsService svc, CancellationToken ct) =>
        {
            var stats = await svc.GetSummaryAsync(ct);
            return Results.Ok(stats);
        });

        // GET /api/stats/reports-today
        group.MapGet("/reports-today", async (StatsService svc, CancellationToken ct) =>
        {
            var reports = await svc.GetTodayReportsAsync(ct);
            return Results.Ok(reports);
        });

        // GET /api/stats/affected-stations
        group.MapGet("/affected-stations", async (StatsService svc, CancellationToken ct) =>
        {
            var stations = await svc.GetAffectedStationsAsync(ct);
            return Results.Ok(stations);
        });
    }
}
