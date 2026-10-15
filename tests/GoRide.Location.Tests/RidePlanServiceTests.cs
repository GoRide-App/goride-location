using GoRide.Location.Models;
using GoRide.Location.Services;
using Xunit;

namespace GoRide.Location.Tests;

public class RidePlanServiceTests
{
    [Fact]
    public void ParseOrsResponse_ExtractsDistanceDurationAndObjectCoordinates()
    {
        // Sample ORS GeoJSON response structure
        var sampleJson = """
        {
            "features": [
                {
                    "properties": {
                        "summary": {
                            "distance": 4500.0,
                            "duration": 600.0
                        }
                    },
                    "geometry": {
                        "coordinates": [
                            [79.8500, 6.9200],
                            [79.8550, 6.9250],
                            [79.8600, 6.9300]
                        ]
                    }
                }
            ]
        }
        """;

        var (distanceKm, durationMinutes, coordinates) = RidePlanService.ParseOrsResponse(sampleJson);

        Assert.Equal(4.5, distanceKm);
        Assert.Equal(10.0, durationMinutes);
        Assert.Equal(3, coordinates.Count);

        // Verify object format { lat, lng }
        Assert.Equal(6.9200, coordinates[0].Lat);
        Assert.Equal(79.8500, coordinates[0].Lng);
        Assert.Equal(6.9300, coordinates[2].Lat);
        Assert.Equal(79.8600, coordinates[2].Lng);
    }

    [Fact]
    public void ServiceableAreaOptions_ValidatesInsideAndOutsideColombo()
    {
        var area = new ServiceableAreaOptions
        {
            MinLat = 6.73,
            MaxLat = 7.10,
            MinLng = 79.79,
            MaxLng = 80.16
        };

        // Colombo Fort (Inside)
        Assert.True(area.Contains(6.93, 79.84));

        // Kandy (Outside)
        Assert.False(area.Contains(7.29, 80.63));
    }
}
