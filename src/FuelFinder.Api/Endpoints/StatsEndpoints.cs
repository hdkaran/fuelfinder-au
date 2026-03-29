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
    }
}
