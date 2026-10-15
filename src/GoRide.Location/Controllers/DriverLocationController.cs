using GoRide.Location.Models;
using GoRide.Location.Services;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace GoRide.Location.Controllers;

/// <summary>
/// Stores and serves drivers' live location + availability. A driver is
/// "available for trips" precisely when their latest row here has
/// Status == "Online" — see <see cref="DriverLocation"/>.
/// </summary>
[ApiController]
[Route("location")]
public class DriverLocationController : ControllerBase
{
    private readonly IDriverLocationService _service;
    private readonly ILogger<DriverLocationController> _logger;

    public DriverLocationController(IDriverLocationService service, ILogger<DriverLocationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Upserts a driver's location and status. Called by the frontend on every
    /// GPS update, on manual pin placement, and whenever the driver toggles
    /// their Available switch.
    /// </summary>
    [HttpPost("update")]
    [ProducesResponseType(typeof(DriverLocation), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Update([FromBody] UpdateDriverLocationRequest request, CancellationToken ct)
    {
        try
        {
            var (result, error) = await _service.UpdateAsync(request, ct);
            if (error is not null)
                return Problem(title: "Invalid location update", detail: error, statusCode: StatusCodes.Status400BadRequest);

            _logger.LogInformation("Location updated for driver {DriverId}: status={Status}", request.DriverId, request.Status);
            return Ok(result);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error while updating location for driver {DriverId}", request.DriverId);
            return Problem(title: "Location store unavailable", detail: "Unable to reach the location database. Please try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Drivers currently Online (available) within radiusKm of (lat, lng), nearest first.</summary>
    [HttpGet("nearby-drivers")]
    [ProducesResponseType(typeof(IReadOnlyList<DriverLocation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> NearbyDrivers([FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm, CancellationToken ct)
    {
        try
        {
            var (result, error) = await _service.GetNearbyAvailableAsync(lat, lng, radiusKm, ct);
            if (error is not null)
                return Problem(title: "Invalid query", detail: error, statusCode: StatusCodes.Status400BadRequest);

            return Ok(result);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error while listing nearby drivers");
            return Problem(title: "Location store unavailable", detail: "Unable to reach the location database. Please try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>A specific driver's last known location, or 404 if never reported.</summary>
    [HttpGet("{driverId}")]
    [ProducesResponseType(typeof(DriverLocation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(string driverId, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetAsync(driverId, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error while fetching location for driver {DriverId}", driverId);
            return Problem(title: "Location store unavailable", detail: "Unable to reach the location database. Please try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
