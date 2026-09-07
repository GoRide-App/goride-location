using GoRide.Location.Models;

namespace GoRide.Location.Services;

/// <summary>
/// Plans a ride by validating coordinates against the serviceable area,
/// calling ORS for real road-network routing, and computing a fare estimate.
/// </summary>
public interface IRidePlanService
{
    /// <summary>
    /// Plans a ride from pickup to destination.
    /// </summary>
    /// <param name="request">Pickup and destination coordinates.</param>
    /// <returns>
    /// A tuple where <c>Result</c> is populated on success and <c>Error</c> is null,
    /// or <c>Result</c> is null and <c>Error</c> contains a user-facing description of
    /// what went wrong (validation failure or upstream ORS failure).
    /// </returns>
    Task<(RidePlanResponse? Result, string? Error)> PlanRideAsync(RidePlanRequest request);
}
