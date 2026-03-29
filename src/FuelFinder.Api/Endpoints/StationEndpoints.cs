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

        // GET /api/stations/{id}
        group.MapGet("/{id:guid}", async (
            Guid id, StationQueryService svc, CancellationToken ct) =>
        {
            var station = await svc.GetByIdAsync(id, ct);
            return station is null ? Results.NotFound() : Results.Ok(station);
        });
    }
}
