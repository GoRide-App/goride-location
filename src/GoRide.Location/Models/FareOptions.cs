namespace GoRide.Location.Models;

/// <summary>
/// Fare calculation parameters. Fare (PHP) = BaseFare + (distanceKm × RatePerKm).
/// Both values are configurable so product can adjust pricing without a deployment.
/// </summary>
public class FareOptions
{
    public const string SectionName = "Fare";

    /// <summary>Fixed flag-down fare in PHP.</summary>
    public decimal BaseFare { get; init; } = 40m;

    /// <summary>Per-kilometre rate in PHP.</summary>
    public decimal RatePerKm { get; init; } = 13m;
}
