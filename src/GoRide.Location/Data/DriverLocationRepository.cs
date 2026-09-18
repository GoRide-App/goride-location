using GoRide.Location.Models;
using MySqlConnector;

namespace GoRide.Location.Data;

public class DriverLocationRepository : IDriverLocationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DriverLocationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS driver_locations (
                driver_id  VARCHAR(64)  NOT NULL PRIMARY KEY,
                lat        DOUBLE       NOT NULL,
                lng        DOUBLE       NOT NULL,
                heading    DOUBLE       NOT NULL DEFAULT 0,
                status     VARCHAR(16)  NOT NULL DEFAULT 'Offline',
                updated_at DATETIME(3)  NOT NULL,
                INDEX idx_driver_locations_status_updated (status, updated_at)
            )
            """;

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<DriverLocation> UpsertAsync(string driverId, double lat, double lng, double heading, string status, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO driver_locations (driver_id, lat, lng, heading, status, updated_at)
            VALUES (@driverId, @lat, @lng, @heading, @status, UTC_TIMESTAMP(3))
            ON DUPLICATE KEY UPDATE
                lat = VALUES(lat),
                lng = VALUES(lng),
                heading = VALUES(heading),
                status = VALUES(status),
                updated_at = VALUES(updated_at)
            """;

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        await using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@driverId", driverId);
            cmd.Parameters.AddWithValue("@lat", lat);
            cmd.Parameters.AddWithValue("@lng", lng);
            cmd.Parameters.AddWithValue("@heading", heading);
            cmd.Parameters.AddWithValue("@status", status);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (await GetAsync(driverId, ct))!;
    }

    public async Task<DriverLocation?> GetAsync(string driverId, CancellationToken ct = default)
    {
        const string sql = "SELECT driver_id, lat, lng, heading, status, updated_at FROM driver_locations WHERE driver_id = @driverId";

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@driverId", driverId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<DriverLocation>> GetNearbyAvailableAsync(double lat, double lng, double radiusKm, CancellationToken ct = default)
    {
        // Haversine distance in km. Clamp the ACOS argument to [-1, 1] — floating-point
        // rounding can push it a hair outside that range for near-identical points,
        // which would otherwise make ACOS return NaN.
        const string sql = """
            SELECT driver_id, lat, lng, heading, status, updated_at
            FROM (
                SELECT *,
                    6371 * ACOS(LEAST(1, GREATEST(-1,
                        COS(RADIANS(@lat)) * COS(RADIANS(lat)) * COS(RADIANS(lng) - RADIANS(@lng))
                        + SIN(RADIANS(@lat)) * SIN(RADIANS(lat))
                    ))) AS distance_km
                FROM driver_locations
                WHERE status = 'Online'
                  AND updated_at >= (UTC_TIMESTAMP(3) - INTERVAL 2 MINUTE)
            ) nearby
            WHERE distance_km <= @radiusKm
            ORDER BY distance_km ASC
            """;

        await using var conn = _connectionFactory.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@lat", lat);
        cmd.Parameters.AddWithValue("@lng", lng);
        cmd.Parameters.AddWithValue("@radiusKm", radiusKm);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<DriverLocation>();
        while (await reader.ReadAsync(ct)) results.Add(Map(reader));
        return results;
    }

    private static DriverLocation Map(MySqlDataReader reader) => new()
    {
        DriverId = reader.GetString(0),
        Lat = reader.GetDouble(1),
        Lng = reader.GetDouble(2),
        Heading = reader.GetDouble(3),
        Status = reader.GetString(4),
        UpdatedAt = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
    };
}
