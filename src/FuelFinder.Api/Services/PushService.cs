using FuelFinder.Api.Data;
using FuelFinder.Api.Dtos;
using FuelFinder.Api.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace FuelFinder.Api.Services;

sealed class PushService(AppDbContext db, IConfiguration config, ILogger<PushService> logger)
{
    private const double NotifyRadiusMetres = 5_000;

    // ── Subscribe ─────────────────────────────────────────────────────────────

    public async Task SubscribeAsync(PushSubscribePayload payload, CancellationToken ct)
    {
        var existing = await db.PushRegistrations
            .FirstOrDefaultAsync(p => p.Endpoint == payload.Endpoint, ct);

        if (existing is not null)
        {
            // Update location in case the user has moved
            existing.Latitude  = payload.Latitude;
            existing.Longitude = payload.Longitude;
        }
        else
        {
            db.PushRegistrations.Add(new PushRegistration
            {
                Endpoint  = payload.Endpoint,
                P256dh    = payload.P256dh,
                Auth      = payload.Auth,
                Latitude  = payload.Latitude,
                Longitude = payload.Longitude,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Unsubscribe ───────────────────────────────────────────────────────────

    public async Task UnsubscribeAsync(string endpoint, CancellationToken ct)
    {
        var reg = await db.PushRegistrations
            .FirstOrDefaultAsync(p => p.Endpoint == endpoint, ct);

        if (reg is not null)
        {
            db.PushRegistrations.Remove(reg);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Notify ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a push notification to all subscribers within 5 km of the station
    /// that just received a new report.
    /// Fire-and-forget: errors are logged but never thrown to the caller.
    /// </summary>
    public async Task NotifyNearbyAsync(Station station, string reportStatus, CancellationToken ct)
    {
        var client = BuildClient();
        if (client is null) return;

        var nearby = await GetNearbyRegistrationsAsync(station.Latitude, station.Longitude, ct);
        if (nearby.Count == 0) return;

        var statusLabel = reportStatus switch
        {
            "available" => "✅ Fuel available",
            "low"       => "⚠️ Fuel running low",
            "out"       => "❌ Fuel out",
            "queue"     => "🚗 Long queue",
            _           => "📢 New report",
        };

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "FuelFinder AU",
            body  = $"{statusLabel} at {station.Name}, {station.Suburb}",
            url   = "/",
        });

        var toDelete = new List<PushRegistration>();

        foreach (var reg in nearby)
        {
            try
            {
                var sub = new WebPush.PushSubscription(reg.Endpoint, reg.P256dh, reg.Auth);
                await client.SendNotificationAsync(sub, payload);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // Subscription has expired — clean it up
                toDelete.Add(reg);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send push to {Endpoint}", reg.Endpoint);
            }
        }

        if (toDelete.Count > 0)
        {
            db.PushRegistrations.RemoveRange(toDelete);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<PushRegistration>> GetNearbyRegistrationsAsync(
        double lat, double lng, CancellationToken ct)
    {
        var latDelta = NotifyRadiusMetres / 111_000.0;
        var lngDelta = NotifyRadiusMetres / (111_000.0 * Math.Cos(lat * Math.PI / 180.0));

        var candidates = await db.PushRegistrations
            .Where(p =>
                p.Latitude  >= lat - latDelta && p.Latitude  <= lat + latDelta &&
                p.Longitude >= lng - lngDelta && p.Longitude <= lng + lngDelta)
            .AsNoTracking()
            .ToListAsync(ct);

        return candidates
            .Where(p => Haversine(lat, lng, p.Latitude, p.Longitude) <= NotifyRadiusMetres)
            .ToList();
    }

    private WebPushClient? BuildClient()
    {
        var subject    = config["Vapid:Subject"];
        var publicKey  = config["Vapid:PublicKey"];
        var privateKey = config["Vapid:PrivateKey"];

        if (string.IsNullOrEmpty(subject) ||
            string.IsNullOrEmpty(publicKey) ||
            string.IsNullOrEmpty(privateKey))
        {
            logger.LogDebug("VAPID keys not configured — push notifications disabled.");
            return null;
        }

        var client = new WebPushClient();
        client.SetVapidDetails(subject, publicKey, privateKey);
        return client;
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
