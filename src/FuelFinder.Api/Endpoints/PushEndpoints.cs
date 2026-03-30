using FuelFinder.Api.Dtos;
using FuelFinder.Api.Services;

namespace FuelFinder.Api.Endpoints;

static class PushEndpoints
{
    internal static void MapPushEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/push");

        // POST /api/push/subscribe
        group.MapPost("/subscribe", async (
            PushSubscribePayload payload, PushService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.Endpoint) ||
                string.IsNullOrWhiteSpace(payload.P256dh) ||
                string.IsNullOrWhiteSpace(payload.Auth))
                return Results.BadRequest(new { error = "endpoint, p256dh and auth are required." });

            await svc.SubscribeAsync(payload, ct);
            return Results.Ok();
        });

        // DELETE /api/push/subscribe
        group.MapDelete("/subscribe", async (
            string endpoint, PushService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return Results.BadRequest(new { error = "endpoint is required." });

            await svc.UnsubscribeAsync(endpoint, ct);
            return Results.NoContent();
        });
    }
}
