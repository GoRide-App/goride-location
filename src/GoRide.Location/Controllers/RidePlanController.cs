using GoRide.Location.Models;
using GoRide.Location.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoRide.Location.Controllers;

/// <summary>
/// Handles ride-planning requests from the Next.js frontend.
/// POST /rides/plan is the single endpoint called when a rider submits their
/// pickup and destination on the Ride Planning screen.
/// </summary>
[ApiController]
[Route("rides")]
public class RidePlanController : ControllerBase
{
    private readonly IRidePlanService _ridePlanService;
    private readonly ILogger<RidePlanController> _logger;

    public RidePlanController(IRidePlanService ridePlanService, ILogger<RidePlanController> logger)
    {
        _ridePlanService = ridePlanService;
        _logger = logger;
    }

    /// <summary>
    /// Plans a ride by validating coordinates, calling ORS for routing,
    /// and returning distance, duration and fare estimate.
    /// </summary>
    /// <param name="request">Pickup and destination coordinates.</param>
    /// <returns>
    /// 200 with <see cref="RidePlanResponse"/> on success.<br/>
    /// 400 with ProblemDetails when the request is invalid or coordinates are outside the serviceable area.<br/>
    /// 502 with ProblemDetails when the upstream routing service (ORS) is unavailable.
    /// </returns>
    [HttpPost("plan")]
    [ProducesResponseType(typeof(RidePlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> PlanRide([FromBody] RidePlanRequest request)
    {
        // Model-binding errors (missing fields, out-of-range values) are caught
        // automatically by [ApiController] and returned as 400 before we get here.

        _logger.LogInformation(
            "PlanRide requested: pickup=({PickupLat},{PickupLng}) dest=({DestLat},{DestLng})",
            request.PickupLat, request.PickupLng, request.DestinationLat, request.DestinationLng);

        var (result, error) = await _ridePlanService.PlanRideAsync(request);

        if (error is not null)
        {
            // Distinguish upstream (ORS) failures from validation failures.
            // Validation errors produced by the service always contain "outside the GoRide serviceable area".
            var isUpstreamError =
                error.Contains("routing service", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("Unable to reach", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("unexpected error", StringComparison.OrdinalIgnoreCase);

            if (isUpstreamError)
            {
                return Problem(
                    title: "Routing service unavailable",
                    detail: error,
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Problem(
                title: "Invalid ride plan request",
                detail: error,
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(result);
    }
}
