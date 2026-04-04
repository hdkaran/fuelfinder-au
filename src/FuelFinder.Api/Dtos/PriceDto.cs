namespace FuelFinder.Api.Dtos;

public record PriceDto(
    Guid            StationId,
    string          StationName,
    string          Brand,
    string          Address,
    string          Suburb,
    double          DistanceMetres,
    string          FuelType,
    decimal         PricePerLitreCents,
    DateTimeOffset  RecordedAtUtc,
    bool            IsStale);
