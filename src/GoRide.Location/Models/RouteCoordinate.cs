namespace GoRide.Location.Models;

/// <summary>
/// Geographic coordinate point in object format { lat, lng }.
/// Matches the frontend LatLng model for Leaflet polyline rendering.
/// </summary>
public class RouteCoordinate
{
    public double Lat { get; init; }
    public double Lng { get; init; }
}
