using System.Net.Http.Headers;
using System.Text.Json;
using GoRide.Location.Models;
using Microsoft.Extensions.Options;

namespace GoRide.Location.Services;

/// <summary>
/// Implements ride planning:
///   1. Validates that both coordinates are inside the configured serviceable area.
///   2. Calls the OpenRouteService (ORS) Directions API for real road-network
///      distance (km) and duration (minutes).
///   3. Calculates fare = BaseFare + (distanceKm × RatePerKm).
///
/// ORS API shape (GET /v2/directions/driving-car):
///   Request  : query params start=lng,lat  end=lng,lat
///   Auth     : Authorization: Bearer {ApiKey}
///   Response : { "routes": [{ "summary": { "distance": <metres>, "duration": <seconds> } }] }
/// </summary>
public class RidePlanService : IRidePlanService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OrsOptions _ors;
    private readonly ServiceableAreaOptions _area;
    private readonly FareOptions _fare;
    private readonly ILogger<RidePlanService> _logger;

    public RidePlanService(
        IHttpClientFactory httpClientFactory,
        IOptions<OrsOptions> ors,
        IOptions<ServiceableAreaOptions> area,
        IOptions<FareOptions> fare,
        ILogger<RidePlanService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ors = ors.Value;
        _area = area.Value;
        _fare = fare.Value;
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

        // ── 2. ORS routing call ─────────────────────────────────────────────
        double distanceKm;
        double durationMinutes;

        try
        {
            (distanceKm, durationMinutes) = await GetRouteFromOrsAsync(
                request.PickupLng, request.PickupLat,
                request.DestinationLng, request.DestinationLat);
        }
        catch (OrsException ex)
        {
            _logger.LogError(ex, "ORS routing call failed for request {@Request}", request);
            return (null, ex.Message);
        }

        // ── 3. Fare calculation ─────────────────────────────────────────────
        var fareEstimate = _fare.BaseFare + ((decimal)distanceKm * _fare.RatePerKm);

        var response = new RidePlanResponse
        {
            DistanceKm = Math.Round(distanceKm, 2),
            DurationMinutes = Math.Round(durationMinutes, 1),
            FareEstimate = Math.Round(fareEstimate, 2),
            PickupLabel = $"{request.PickupLat:F6}, {request.PickupLng:F6}",
            DestinationLabel = $"{request.DestinationLat:F6}, {request.DestinationLng:F6}",
        };

        return (response, null);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Calls ORS GET /v2/directions/driving-car and extracts distance (km) and duration (min).
    /// Throws <see cref="OrsException"/> on any HTTP or parsing error.
    /// </summary>
    private async Task<(double DistanceKm, double DurationMinutes)> GetRouteFromOrsAsync(
        double startLng, double startLat, double endLng, double endLat)
    {
        // ORS expects coordinates as lng,lat (not lat,lng)
        var url = $"{_ors.BaseUrl.TrimEnd('/')}/v2/directions/driving-car" +
                  $"?start={startLng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                  $",{startLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&end={endLng.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                  $",{endLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";

        using var client = _httpClientFactory.CreateClient("OrsClient");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _ors.ApiKey);
        // ORS GET /directions only accepts application/geo+json (not application/json)
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/geo+json"));

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await client.GetAsync(url);
        }
        catch (HttpRequestException ex)
        {
            throw new OrsException("Unable to reach the routing service. Please try again later.", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("ORS returned {StatusCode}: {Body}", (int)httpResponse.StatusCode, body);
            throw new OrsException(
                $"Routing service returned an unexpected error (HTTP {(int)httpResponse.StatusCode}). " +
                "Please try again later.");
        }

        var json = await httpResponse.Content.ReadAsStringAsync();
        return ParseOrsResponse(json);
    }

    /// <summary>
    /// Parses the ORS application/geo+json response and returns (distanceKm, durationMinutes).
    /// ORS geo+json shape:
    ///   { "features": [{ "properties": { "summary": { "distance": &lt;metres&gt;, "duration": &lt;seconds&gt; } } }] }
    /// Throws <see cref="OrsException"/> if the expected structure is absent.
    /// </summary>
    public static (double DistanceKm, double DurationMinutes) ParseOrsResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // ORS geo+json wraps routes inside "features"
        if (!root.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
            throw new OrsException("Routing service returned no routes for the given coordinates.");

        var properties = features[0].GetProperty("properties");
        var summary = properties.GetProperty("summary");
        var distanceMetres = summary.GetProperty("distance").GetDouble();
        var durationSeconds = summary.GetProperty("duration").GetDouble();

        return (distanceMetres / 1000.0, durationSeconds / 60.0);
    }
}

/// <summary>
/// Represents a failure when communicating with or interpreting responses from ORS.
/// Kept internal to the service layer; the controller maps it to a 502 response.
/// </summary>
public sealed class OrsException : Exception
{
    public OrsException(string message) : base(message) { }
    public OrsException(string message, Exception inner) : base(message, inner) { }
}
