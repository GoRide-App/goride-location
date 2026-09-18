using GoRide.Location.Models;

namespace GoRide.Location.Services;

public interface IDriverLocationService
{
    Task<(DriverLocation? Result, string? Error)> UpdateAsync(UpdateDriverLocationRequest request, CancellationToken ct = default);

    Task<DriverLocation?> GetAsync(string driverId, CancellationToken ct = default);

    Task<(IReadOnlyList<DriverLocation>? Result, string? Error)> GetNearbyAvailableAsync(double lat, double lng, double radiusKm, CancellationToken ct = default);
}
