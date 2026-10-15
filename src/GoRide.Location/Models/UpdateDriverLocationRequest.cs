namespace GoRide.Location.Models;

/// <summary>
/// Body of POST /location/update. Matches the frontend's existing
/// api.location.updateDriverLocation(driverId, pos, heading, status) call.
/// </summary>
public class UpdateDriverLocationRequest
{
    public string DriverId { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Heading { get; set; }
    public string Status { get; set; } = "Offline";
}
