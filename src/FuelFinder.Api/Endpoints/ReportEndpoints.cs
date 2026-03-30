using FuelFinder.Api.Dtos;
using FuelFinder.Api.Services;

namespace FuelFinder.Api.Endpoints;

static class ReportEndpoints
{
    private static readonly HashSet<string> ValidStatuses = ["available", "low", "out", "queue"];
    private static readonly HashSet<string> ValidFuelTypes = ["Diesel", "ULP", "E10", "Premium"];

    internal static void MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/reports");

        // POST /api/reports
        group.MapPost("/", async (
            ReportPayload payload, ReportService svc, CancellationToken ct) =>
        {
            if (!ValidStatuses.Contains(payload.Status))
                return Results.BadRequest(new { error = $"Invalid status '{payload.Status}'." });

            if (payload.FuelTypes.Any(ft => !ValidFuelTypes.Contains(ft.FuelType)))
                return Results.BadRequest(new { error = "Invalid fuel type in fuelTypes." });

            var id = await svc.SubmitAsync(payload, ct);
            return id is null
                ? Results.NotFound(new { error = $"Station {payload.StationId} not found." })
                : Results.Created($"/api/reports/{id}", null);
        }).RequireRateLimiting("reports");

        // GET /api/reports/recent?stationId=
        group.MapGet("/recent", async (
            Guid stationId, ReportService svc, CancellationToken ct) =>
        {
            var reports = await svc.GetRecentAsync(stationId, ct);
            return Results.Ok(reports);
        });
    }
}
