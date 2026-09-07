using System.ComponentModel.DataAnnotations;

namespace GoRide.Location.Models;

/// <summary>
/// Input DTO for POST /rides/plan.
/// The rider supplies the coordinates of their pickup and destination;
/// the service validates them against the serviceable area and then
/// calls ORS for real road-network routing.
/// </summary>
public class RidePlanRequest
{
    [Required]
    [Range(-90, 90, ErrorMessage = "PickupLat must be a valid latitude (-90 to 90).")]
    public double PickupLat { get; init; }

    [Required]
    [Range(-180, 180, ErrorMessage = "PickupLng must be a valid longitude (-180 to 180).")]
    public double PickupLng { get; init; }

    [Required]
    [Range(-90, 90, ErrorMessage = "DestinationLat must be a valid latitude (-90 to 90).")]
    public double DestinationLat { get; init; }

    [Required]
    [Range(-180, 180, ErrorMessage = "DestinationLng must be a valid longitude (-180 to 180).")]
    public double DestinationLng { get; init; }
}
