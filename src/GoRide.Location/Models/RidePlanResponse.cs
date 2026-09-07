namespace GoRide.Location.Models;

/// <summary>
/// Response DTO returned by POST /rides/plan on success.
/// Contains the ORS-calculated road-network distance/duration and the
/// computed fare estimate based on the configured base fare and per-km rate.
/// </summary>
public class RidePlanResponse
{
    /// <summary>Road-network distance in kilometres (from ORS, metres ÷ 1000).</summary>
    public double DistanceKm { get; init; }

    /// <summary>Estimated travel time in minutes (from ORS, seconds ÷ 60).</summary>
    public double DurationMinutes { get; init; }

    /// <summary>Fare estimate in PHP = BaseFare + (DistanceKm × RatePerKm).</summary>
    public decimal FareEstimate { get; init; }

    /// <summary>Human-readable label echoing back the pickup coordinates.</summary>
    public string PickupLabel { get; init; } = string.Empty;

    /// <summary>Human-readable label echoing back the destination coordinates.</summary>
    public string DestinationLabel { get; init; } = string.Empty;
}
