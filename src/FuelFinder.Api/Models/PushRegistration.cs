namespace FuelFinder.Api.Models;

/// <summary>
/// Stores a browser Push API subscription along with the subscriber's location
/// so we can notify them when a report is submitted within their radius.
/// </summary>
public class PushRegistration
{
    public int Id { get; set; }

    /// <summary>Push service endpoint URL provided by the browser.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Browser-generated P-256 public key (base64url).</summary>
    public string P256dh { get; set; } = "";

    /// <summary>Browser-generated auth secret (base64url).</summary>
    public string Auth { get; set; } = "";

    /// <summary>Subscriber's latitude at time of subscription.</summary>
    public double Latitude { get; set; }

    /// <summary>Subscriber's longitude at time of subscription.</summary>
    public double Longitude { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
