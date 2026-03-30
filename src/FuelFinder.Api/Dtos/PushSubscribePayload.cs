namespace FuelFinder.Api.Dtos;

public record PushSubscribePayload(
    string Endpoint,
    string P256dh,
    string Auth,
    double Latitude,
    double Longitude
);
