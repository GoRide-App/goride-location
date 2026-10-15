namespace GoRide.Location.Models;

/// <summary>
/// Configuration for the OpenRouteService (ORS) HTTP client.
/// Non-secret values (BaseUrl) live in appsettings.json.
/// The API key is intentionally kept out of source control:
///   - Locally: appsettings.Development.json  (gitignored)
///   - Docker / Azure: Ors__ApiKey environment variable
/// </summary>
public class OrsOptions
{
    public const string SectionName = "Ors";

    /// <summary>ORS base URL, e.g. https://api.openrouteservice.org</summary>
    public string BaseUrl { get; init; } = "https://api.openrouteservice.org";

    /// <summary>ORS Bearer token — must be provided via secret config, never committed.</summary>
    public string ApiKey { get; init; } = string.Empty;
}
