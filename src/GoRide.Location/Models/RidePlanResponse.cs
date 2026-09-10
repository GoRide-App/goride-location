namespace GoRide.Location.Models;

/// <summary>
/// Response DTO returned by POST /rides/plan on success.
/// Contains road-network distance/duration from routing provider (ORS/OSRM)
/// and the route geometry coordinates in object format { lat, lng }.
/// Fare calculation is deliberately handled by GoRide.Trip / frontend.
/// </summary>
public class RidePlanResponse
{
    /// <summary>Road-network distance in kilometres (from routing provider, metres ÷ 1000).</summary>
    public double DistanceKm { get; init; }

    /// <summary>Estimated travel time in minutes (from routing provider, seconds ÷ 60).</summary>
    public double DurationMinutes { get; init; }

    /// <summary>Route geometry coordinates in object format [{ lat, lng }, ...].</summary>
    public List<RouteCoordinate> Coordinates { get; init; } = new();

    /// <summary>Human-readable label echoing back the pickup coordinates.</summary>
    public string PickupLabel { get; init; } = string.Empty;

    /// <summary>Human-readable label echoing back the destination coordinates.</summary>
    public string DestinationLabel { get; init; } = string.Empty;
}
