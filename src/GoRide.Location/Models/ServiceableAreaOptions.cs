namespace GoRide.Location.Models;

/// <summary>
/// Bounding-box configuration that defines the geographic region this GoRide
/// deployment serves. Both pickup and destination coordinates must fall inside
/// these bounds before a routing call is made.
///
/// Default values cover a ~20 km radius around SLIIT Malabe, Colombo, Sri Lanka
/// (centre: 6.9147°N, 79.9726°E). Each degree of latitude ≈ 111 km;
/// each degree of longitude at this latitude ≈ 110.2 km.
/// Override per environment via appsettings.{Environment}.json or env vars.
/// </summary>
public class ServiceableAreaOptions
{
    public const string SectionName = "ServiceableArea";

    // ~20 km radius from SLIIT Malabe (6.9147°N, 79.9726°E)
    public double MinLat { get; init; } = 6.73;
    public double MaxLat { get; init; } = 7.10;
    public double MinLng { get; init; } = 79.79;
    public double MaxLng { get; init; } = 80.16;

    /// <summary>Returns true when the supplied point is inside the bounding box.</summary>
    public bool Contains(double lat, double lng) =>
        lat >= MinLat && lat <= MaxLat &&
        lng >= MinLng && lng <= MaxLng;
}
