using GoRide.Location.Models;

namespace GoRide.Location.Data;

public interface IDriverLocationRepository
{
    /// <summary>Idempotent — creates the driver_locations table if it doesn't already exist.</summary>
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<DriverLocation> UpsertAsync(string driverId, double lat, double lng, double heading, string status, CancellationToken ct = default);

    Task<DriverLocation?> GetAsync(string driverId, CancellationToken ct = default);

    /// <summary>Drivers currently marked "Online" within radiusKm of (lat, lng), nearest first.</summary>
    Task<IReadOnlyList<DriverLocation>> GetNearbyAvailableAsync(double lat, double lng, double radiusKm, CancellationToken ct = default);
}
