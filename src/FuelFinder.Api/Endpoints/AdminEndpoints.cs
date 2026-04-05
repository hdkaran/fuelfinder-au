using FuelFinder.Api.Services;

namespace FuelFinder.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var adminEnabled = app.Configuration.GetValue<bool>("Admin:Enabled", false);
        if (!adminEnabled) return;

        app.MapPost("/api/admin/sync/nsw", async (IPriceSyncService svc, CancellationToken ct) =>
        {
            await svc.SyncNswForceFullAsync(ct);
            return Results.Ok(new { message = "NSW full sync triggered" });
        });
    }
}
