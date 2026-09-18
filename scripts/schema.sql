-- goride-location schema
--
-- This is a manual copy of the same DDL the app runs automatically on every
-- startup (GoRide.Location.Data.DriverLocationRepository.EnsureSchemaAsync,
-- called from Program.cs) — this service has no migration tool, so schema is
-- kept idempotent (CREATE TABLE IF NOT EXISTS) rather than versioned.
--
-- You only need to run this by hand if you want the table to exist before
-- the app's first successful DB connection (e.g. to inspect it, or because
-- credentials weren't working yet when the app last started). Otherwise,
-- just fixing the DB credentials and starting the service is enough.
--
-- Run against the location_db database on the Azure MySQL server, e.g.:
--   mysql -h goride-dbv2.mysql.database.azure.com -P 3306 -u location_svc -p location_db < scripts/schema.sql

CREATE TABLE IF NOT EXISTS driver_locations (
    driver_id  VARCHAR(64)  NOT NULL PRIMARY KEY,
    lat        DOUBLE       NOT NULL,
    lng        DOUBLE       NOT NULL,
    heading    DOUBLE       NOT NULL DEFAULT 0,
    status     VARCHAR(16)  NOT NULL DEFAULT 'Offline',
    updated_at DATETIME(3)  NOT NULL,
    INDEX idx_driver_locations_status_updated (status, updated_at)
);
