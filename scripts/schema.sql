-- goride-location schema
--
-- Run this against the location_db database using the Azure MySQL server's ADMIN
-- login. The service's own user (location_svc) is scoped to reading and writing
-- location_db and normally can't create tables.
--
-- The app also tries this same DDL on startup
-- (GoRide.Location.Data.DriverLocationRepository.EnsureSchemaAsync, called from
-- Program.cs). It only succeeds if the service user has CREATE rights; otherwise
-- it logs "Failed to ensure driver_locations schema exists" and carries on, so
-- running this script by hand is the reliable path. Safe to re-run.

CREATE TABLE IF NOT EXISTS driver_locations (
    driver_id  VARCHAR(64)  NOT NULL PRIMARY KEY,
    lat        DOUBLE       NOT NULL,
    lng        DOUBLE       NOT NULL,
    heading    DOUBLE       NOT NULL DEFAULT 0,
    status     VARCHAR(16)  NOT NULL DEFAULT 'Offline',
    updated_at DATETIME(3)  NOT NULL,
    INDEX idx_driver_locations_status_updated (status, updated_at)
);
