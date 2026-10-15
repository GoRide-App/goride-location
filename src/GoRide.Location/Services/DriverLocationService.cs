using GoRide.Location.Data;
using GoRide.Location.Models;

namespace GoRide.Location.Services;

public class DriverLocationService : IDriverLocationService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase) { "Online", "Offline", "OnTrip" };

    private readonly IDriverLocationRepository _repository;

    public DriverLocationService(IDriverLocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<(DriverLocation? Result, string? Error)> UpdateAsync(UpdateDriverLocationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DriverId))
            return (null, "driverId is required.");

        if (request.Lat is < -90 or > 90)
            return (null, "lat must be between -90 and 90.");

        if (request.Lng is < -180 or > 180)
            return (null, "lng must be between -180 and 180.");

        if (!ValidStatuses.Contains(request.Status))
            return (null, $"status must be one of: {string.Join(", ", ValidStatuses)}.");

        var saved = await _repository.UpsertAsync(request.DriverId, request.Lat, request.Lng, request.Heading, request.Status, ct);
        return (saved, null);
    }

    public Task<DriverLocation?> GetAsync(string driverId, CancellationToken ct = default) =>
        _repository.GetAsync(driverId, ct);

    public async Task<(IReadOnlyList<DriverLocation>? Result, string? Error)> GetNearbyAvailableAsync(double lat, double lng, double radiusKm, CancellationToken ct = default)
    {
        if (lat is < -90 or > 90) return (null, "lat must be between -90 and 90.");
        if (lng is < -180 or > 180) return (null, "lng must be between -180 and 180.");

        var effectiveRadius = radiusKm > 0 ? radiusKm : 4;
        var results = await _repository.GetNearbyAvailableAsync(lat, lng, effectiveRadius, ct);
        return (results, null);
    }
}
