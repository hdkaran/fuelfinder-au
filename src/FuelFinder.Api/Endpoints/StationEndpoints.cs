using FuelFinder.Api.Services;

namespace FuelFinder.Api.Endpoints;

static class StationEndpoints
{
    internal static void MapStationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/stations");

        // GET /api/stations/nearby?lat=&lng=&radius=&fuelType=
        group.MapGet("/nearby", async (
            double lat, double lng, double radius, string? fuelType,
            StationQueryService svc, CancellationToken ct) =>
        {
            var stations = await svc.GetNearbyAsync(lat, lng, radius, fuelType, ct);
            return Results.Ok(stations);
        });

        // GET /api/stations/search?q=&lat=&lng=
        group.MapGet("/search", async (
            string q, double? lat, double? lng,
            StationQueryService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Results.BadRequest("q must be at least 2 characters.");
            var stations = await svc.SearchAsync(q, lat, lng, ct);
            return Results.Ok(stations);
        });

        // GET /api/stations/{id}
        group.MapGet("/{id:guid}", async (
            Guid id, StationQueryService svc, CancellationToken ct) =>
        {
            var station = await svc.GetByIdAsync(id, ct);
            return station is null ? Results.NotFound() : Results.Ok(station);
        });
    }
}
