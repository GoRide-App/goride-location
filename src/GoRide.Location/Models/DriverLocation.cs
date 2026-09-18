namespace GoRide.Location.Models;

/// <summary>
/// A driver's last known position and live availability. "Available for trips"
/// is represented by <see cref="Status"/> == "Online" rather than a separate
/// boolean — a driver's availability is inseparable from having a current
/// location, so the two are stored (and upserted) together in one row.
/// </summary>
public class DriverLocation
{
    public string DriverId { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Heading { get; set; }

    /// <summary>One of "Online" (available for trips), "Offline", or "OnTrip".</summary>
    public string Status { get; set; } = "Offline";

    public DateTime UpdatedAt { get; set; }
}
