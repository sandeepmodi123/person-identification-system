-- =============================================================================
-- Person Identification System - Database Schema
-- PostgreSQL 15+
-- =============================================================================

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- =============================================================================
-- Tables
-- =============================================================================

-- Persons
CREATE TABLE IF NOT EXISTS persons (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    risk_level      VARCHAR(20)  NOT NULL DEFAULT 'Medium'
                        CHECK (risk_level IN ('Low', 'Medium', 'High', 'Critical')),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    date_added      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    date_updated    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(255),
    updated_by      VARCHAR(255)
);

-- Person Photos
CREATE TABLE IF NOT EXISTS person_photos (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id           UUID         NOT NULL REFERENCES persons(id) ON DELETE CASCADE,
    photo_url           VARCHAR(500) NOT NULL,
    quality_score       NUMERIC(4,3),
    is_primary          BOOLEAN      NOT NULL DEFAULT FALSE,
    upload_date         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    file_size_bytes     BIGINT,
    original_filename   VARCHAR(255)
);

-- Enforce only one primary photo per person
CREATE UNIQUE INDEX IF NOT EXISTS idx_person_photos_primary
    ON person_photos(person_id)
    WHERE is_primary = TRUE;

-- RTSP Streams
CREATE TABLE IF NOT EXISTS rtsp_streams (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    camera_name             VARCHAR(255)  NOT NULL,
    camera_location         TEXT,
    rtsp_url                VARCHAR(1000) NOT NULL,
    frame_interval_seconds  INT           NOT NULL DEFAULT 5,
    is_active               BOOLEAN       NOT NULL DEFAULT TRUE,
    status                  VARCHAR(20)   NOT NULL DEFAULT 'Unknown'
                                CHECK (status IN ('Online', 'Offline', 'Error', 'Unknown')),
    last_checked            TIMESTAMPTZ,
    date_added              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    date_updated            TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

-- Detections
CREATE TABLE IF NOT EXISTS detections (
    id                    UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_id             UUID        REFERENCES rtsp_streams(id) ON DELETE SET NULL,
    person_id             UUID        REFERENCES persons(id) ON DELETE SET NULL,
    confidence_score      NUMERIC(5,4) NOT NULL,
    detection_timestamp   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    frame_image_url       VARCHAR(500),
    is_verified           BOOLEAN     NOT NULL DEFAULT FALSE,
    verification_status   VARCHAR(20) CHECK (verification_status IN ('TruePositive', 'FalsePositive')),
    verified_by           VARCHAR(255),
    verified_at           TIMESTAMPTZ,
    verification_notes    TEXT,
    email_sent            BOOLEAN     NOT NULL DEFAULT FALSE,
    raw_match_data        JSONB
);

-- Notification Logs
CREATE TABLE IF NOT EXISTS notification_logs (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    detection_id        UUID REFERENCES detections(id) ON DELETE SET NULL,
    recipient_email     VARCHAR(255) NOT NULL,
    sent_timestamp      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    status              VARCHAR(20)  NOT NULL DEFAULT 'Pending'
                            CHECK (status IN ('Pending', 'Sent', 'Failed')),
    error_message       TEXT,
    retry_count         INT          NOT NULL DEFAULT 0,
    message_id          VARCHAR(255)
);

-- Notification Settings (single-row table)
CREATE TABLE IF NOT EXISTS notification_settings (
    id                          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    recipient_emails            TEXT[]      NOT NULL DEFAULT '{}',
    minimum_confidence          NUMERIC(4,3) NOT NULL DEFAULT 0.85,
    notify_on_risk_levels       TEXT[]      NOT NULL DEFAULT '{"High","Critical"}',
    rate_limit_minutes          INT         NOT NULL DEFAULT 5,
    is_enabled                  BOOLEAN     NOT NULL DEFAULT TRUE,
    smtp_host                   VARCHAR(255),
    smtp_port                   INT,
    smtp_use_tls                BOOLEAN,
    from_email                  VARCHAR(255),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Insert default notification settings if none exist
INSERT INTO notification_settings (id)
SELECT uuid_generate_v4()
WHERE NOT EXISTS (SELECT 1 FROM notification_settings);

-- =============================================================================
-- Indexes for performance
-- =============================================================================

-- Fast person lookup by name
CREATE INDEX IF NOT EXISTS idx_persons_name ON persons(name);
CREATE INDEX IF NOT EXISTS idx_persons_is_active ON persons(is_active);
CREATE INDEX IF NOT EXISTS idx_persons_risk_level ON persons(risk_level);

-- Fast photo lookup
CREATE INDEX IF NOT EXISTS idx_person_photos_person_id ON person_photos(person_id);

-- Fast stream lookup
CREATE INDEX IF NOT EXISTS idx_rtsp_streams_is_active ON rtsp_streams(is_active);

-- Fast detection queries
CREATE INDEX IF NOT EXISTS idx_detections_stream_id ON detections(stream_id);
CREATE INDEX IF NOT EXISTS idx_detections_person_id ON detections(person_id);
CREATE INDEX IF NOT EXISTS idx_detections_timestamp ON detections(detection_timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_detections_is_verified ON detections(is_verified);
CREATE INDEX IF NOT EXISTS idx_detections_confidence ON detections(confidence_score);

-- Fast notification log queries
CREATE INDEX IF NOT EXISTS idx_notification_logs_detection_id ON notification_logs(detection_id);
CREATE INDEX IF NOT EXISTS idx_notification_logs_status ON notification_logs(status);
CREATE INDEX IF NOT EXISTS idx_notification_logs_sent_timestamp ON notification_logs(sent_timestamp DESC);

-- GIN index on detection JSONB data for fast JSON queries
CREATE INDEX IF NOT EXISTS idx_detections_raw_match_data ON detections USING GIN(raw_match_data);
