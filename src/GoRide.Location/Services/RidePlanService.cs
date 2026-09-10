using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using GoRide.Location.Models;
using Microsoft.Extensions.Options;

namespace GoRide.Location.Services;

/// <summary>
/// Implements ride planning & routing:
///   1. Validates that pickup and destination are inside the configured serviceable area.
///   2. Calls the OpenRouteService (ORS) Directions API for road-network
///      distance (km), duration (minutes), and geometry coordinates.
///   3. Falls back gracefully to OSRM / geodesic routing when ORS is unconfigured or unavailable.
///   Note: Fare calculation is intentionally handled by GoRide.Trip / frontend.
/// </summary>
public class RidePlanService : IRidePlanService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OrsOptions _ors;
    private readonly ServiceableAreaOptions _area;
    private readonly ILogger<RidePlanService> _logger;

    public RidePlanService(
        IHttpClientFactory httpClientFactory,
        IOptions<OrsOptions> ors,
        IOptions<ServiceableAreaOptions> area,
        ILogger<RidePlanService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ors = ors.Value;
        _area = area.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(RidePlanResponse? Result, string? Error)> PlanRideAsync(RidePlanRequest request)
    {
        // ── 1. Serviceable-area validation ──────────────────────────────────
        if (!_area.Contains(request.PickupLat, request.PickupLng))
        {
            return (null, $"Pickup location ({request.PickupLat:F6}, {request.PickupLng:F6}) " +
                          "is outside the GoRide serviceable area.");
        }

        if (!_area.Contains(request.DestinationLat, request.DestinationLng))
        {
            return (null, $"Destination location ({request.DestinationLat:F6}, {request.DestinationLng:F6}) " +
                          "is outside the GoRide serviceable area.");
        }

        // ── 2. Routing call (ORS with graceful fallback) ─────────────────────
        double distanceKm;
        double durationMinutes;
        List<RouteCoordinate> coordinates;

        try
        {
            (distanceKm, durationMinutes, coordinates) = await GetRouteWithFallbackAsync(
                request.PickupLng, request.PickupLat,
                request.DestinationLng, request.DestinationLat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All routing providers failed for request {@Request}", request);
            return (null, "Unable to reach routing service. Please try again later.");
        }

        var response = new RidePlanResponse
        {
            DistanceKm = Math.Round(distanceKm, 2),
            DurationMinutes = Math.Round(durationMinutes, 1),
            Coordinates = coordinates,
            PickupLabel = $"{request.PickupLat:F6}, {request.PickupLng:F6}",
            DestinationLabel = $"{request.DestinationLat:F6}, {request.DestinationLng:F6}",
        };

        return (response, null);
    }

    // ── Routing logic with fallback ──────────────────────────────────────────

    private async Task<(double DistanceKm, double DurationMinutes, List<RouteCoordinate> Coordinates)>
        GetRouteWithFallbackAsync(double startLng, double startLat, double endLng, double endLat)
    {
        // If ORS API key is configured, attempt primary route via ORS
        if (!string.IsNullOrWhiteSpace(_ors.ApiKey) && !_ors.ApiKey.Contains("${"))
        {
            try
            {
                return await GetRouteFromOrsAsync(startLng, startLat, endLng, endLat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary ORS route failed. Falling back to public OSRM provider.");
            }
        }
        else
        {
            _logger.LogInformation("ORS API key not configured or contains placeholder. Using OSRM routing provider.");
        }

        // Secondary: Public OSRM routing
        try
        {
            return await GetRouteFromOsrmAsync(startLng, startLat, endLng, endLat);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSRM route failed. Falling back to geodesic estimation.");
        }

        // Tertiary fallback: Geodesic Haversine with winding factor
        return EstimateGeodesicRoute(startLng, startLat, endLng, endLat);
    }

    /// <summary>
    /// Calls ORS GET /v2/directions/driving-car and extracts distance (km), duration (min), and coordinates.
    /// </summary>
    private async Task<(double DistanceKm, double DurationMinutes, List<RouteCoordinate> Coordinates)>
        GetRouteFromOrsAsync(double startLng, double startLat, double endLng, double endLat)
    {
        var url = $"{_ors.BaseUrl.TrimEnd('/')}/v2/directions/driving-car" +
                  $"?start={startLng.ToString("F6", CultureInfo.InvariantCulture)}" +
                  $",{startLat.ToString("F6", CultureInfo.InvariantCulture)}" +
                  $"&end={endLng.ToString("F6", CultureInfo.InvariantCulture)}" +
                  $",{endLat.ToString("F6", CultureInfo.InvariantCulture)}";

        using var client = _httpClientFactory.CreateClient("OrsClient");
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ors.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/geo+json"));

        var httpResponse = await client.GetAsync(url);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            throw new OrsException($"ORS returned HTTP {(int)httpResponse.StatusCode}: {body}");
        }

        var json = await httpResponse.Content.ReadAsStringAsync();
        return ParseOrsResponse(json);
    }

    /// <summary>
    /// Parses the ORS application/geo+json response.
    /// Extracts distance, duration, and coordinate objects [{ lat, lng }].
    /// </summary>
    public static (double DistanceKm, double DurationMinutes, List<RouteCoordinate> Coordinates)
        ParseOrsResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
            throw new OrsException("Routing service returned no routes for the given coordinates.");

        var feature = features[0];
        var properties = feature.GetProperty("properties");
        var summary = properties.GetProperty("summary");
        var distanceMetres = summary.GetProperty("distance").GetDouble();
        var durationSeconds = summary.GetProperty("duration").GetDouble();

        var coordinates = new List<RouteCoordinate>();
        if (feature.TryGetProperty("geometry", out var geometry) &&
            geometry.TryGetProperty("coordinates", out var coordsArray))
        {
            foreach (var point in coordsArray.EnumerateArray())
            {
                // GeoJSON format: [longitude, latitude]
                if (point.GetArrayLength() >= 2)
                {
                    coordinates.Add(new RouteCoordinate
                    {
                        Lng = Math.Round(point[0].GetDouble(), 6),
                        Lat = Math.Round(point[1].GetDouble(), 6)
                    });
                }
            }
        }

        return (distanceMetres / 1000.0, durationSeconds / 60.0, coordinates);
    }

    /// <summary>
    /// Calls public OSRM as zero-config fallback.
    /// </summary>
    private async Task<(double DistanceKm, double DurationMinutes, List<RouteCoordinate> Coordinates)>
        GetRouteFromOsrmAsync(double startLng, double startLat, double endLng, double endLat)
    {
        var sLng = startLng.ToString("F6", CultureInfo.InvariantCulture);
        var sLat = startLat.ToString("F6", CultureInfo.InvariantCulture);
        var eLng = endLng.ToString("F6", CultureInfo.InvariantCulture);
        var eLat = endLat.ToString("F6", CultureInfo.InvariantCulture);

        var url = $"https://router.project-osrm.org/route/v1/driving/{sLng},{sLat};{eLng},{eLat}?overview=full&geometries=geojson";

        using var client = _httpClientFactory.CreateClient("OsrmClient");
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GoRide/1.0 (LocationService)");

        var httpResponse = await client.GetAsync(url);
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OSRM returned HTTP {(int)httpResponse.StatusCode}");
        }

        var json = await httpResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OSRM returned no route.");
        }

        var route = routes[0];
        var distanceMetres = route.GetProperty("distance").GetDouble();
        var durationSeconds = route.GetProperty("duration").GetDouble();

        var coordinates = new List<RouteCoordinate>();
        if (route.TryGetProperty("geometry", out var geom) &&
            geom.TryGetProperty("coordinates", out var coordsArray))
        {
            foreach (var point in coordsArray.EnumerateArray())
            {
                if (point.GetArrayLength() >= 2)
                {
                    coordinates.Add(new RouteCoordinate
                    {
                        Lng = Math.Round(point[0].GetDouble(), 6),
                        Lat = Math.Round(point[1].GetDouble(), 6)
                    });
                }
            }
        }

        // Calibrate duration slightly for typical Colombo road traffic
        var durationMin = Math.Max(3.0, (durationSeconds / 60.0) * 1.35);

        return (distanceMetres / 1000.0, durationMin, coordinates);
    }

    /// <summary>
    /// Local offline estimate using Haversine formula with a realistic street curve
    /// so the map never draws an unnatural straight line even if offline.
    /// </summary>
    private static (double DistanceKm, double DurationMinutes, List<RouteCoordinate> Coordinates)
        EstimateGeodesicRoute(double startLng, double startLat, double endLng, double endLat)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = (endLat - startLat) * Math.PI / 180.0;
        var dLng = (endLng - startLng) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(startLat * Math.PI / 180.0) * Math.Cos(endLat * Math.PI / 180.0) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var straightKm = earthRadiusKm * c;

        var roadKm = Math.Max(0.5, straightKm * 1.25);
        var durationMinutes = Math.Max(3.0, (roadKm / 22.0) * 60.0);

        // Gentle street-like curve interpolation (matching frontend syntheticRoute)
        var points = new List<RouteCoordinate>();
        const int steps = 24;
        var dx = endLng - startLng;
        var dy = endLat - startLat;
        var px = -dy;
        var py = dx;

        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var curve = Math.Sin(t * Math.PI) * 0.12;
            var jitter = (i % 4 == 0 ? 0.02 : i % 4 == 2 ? -0.02 : 0) * Math.Sin(t * Math.PI);
            points.Add(new RouteCoordinate
            {
                Lat = Math.Round(startLat + dy * t + py * (curve + jitter), 6),
                Lng = Math.Round(startLng + dx * t + px * (curve + jitter), 6)
            });
        }

        return (roadKm, durationMinutes, points);
    }
}

public sealed class OrsException : Exception
{
    public OrsException(string message) : base(message) { }
    public OrsException(string message, Exception inner) : base(message, inner) { }
}
