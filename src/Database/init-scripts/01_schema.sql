-- ============================================================
-- Person Identification System - Database Schema
-- PostgreSQL 15+
-- ============================================================

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";  -- for fuzzy search

-- ── Persons ─────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS persons (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    risk_level      VARCHAR(20) NOT NULL DEFAULT 'Medium'
                        CHECK (risk_level IN ('Low', 'Medium', 'High', 'Critical')),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    date_added      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    date_updated    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(100),
    updated_by      VARCHAR(100)
);

-- ── Person Photos ────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS person_photos (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    person_id       UUID NOT NULL REFERENCES persons(id) ON DELETE CASCADE,
    photo_url       VARCHAR(500) NOT NULL,
    quality_score   DECIMAL(4,3) CHECK (quality_score >= 0 AND quality_score <= 1),
    is_primary      BOOLEAN NOT NULL DEFAULT FALSE,
    upload_date     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    file_size_bytes BIGINT,
    original_filename VARCHAR(255)
);

-- Only one primary photo per person
CREATE UNIQUE INDEX IF NOT EXISTS uix_person_photos_primary
    ON person_photos (person_id)
    WHERE is_primary = TRUE;

-- ── RTSP Streams ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS rtsp_streams (
    id                      UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    camera_name             VARCHAR(255) NOT NULL,
    camera_location         VARCHAR(500),
    rtsp_url                VARCHAR(1000) NOT NULL,
    frame_interval_seconds  INTEGER NOT NULL DEFAULT 5 CHECK (frame_interval_seconds >= 1),
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    status                  VARCHAR(20) NOT NULL DEFAULT 'Unknown'
                                CHECK (status IN ('Online', 'Offline', 'Error', 'Unknown')),
    last_checked            TIMESTAMPTZ,
    date_added              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    date_updated            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Detections ───────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS detections (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_id           UUID NOT NULL REFERENCES rtsp_streams(id) ON DELETE SET NULL,
    person_id           UUID REFERENCES persons(id) ON DELETE SET NULL,
    confidence_score    DECIMAL(5,4) NOT NULL CHECK (confidence_score >= 0 AND confidence_score <= 1),
    detection_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    frame_image_url     VARCHAR(500),
    is_verified         BOOLEAN NOT NULL DEFAULT FALSE,
    verification_status VARCHAR(20)
                            CHECK (verification_status IN ('TruePositive', 'FalsePositive')),
    verified_by         VARCHAR(100),
    verified_at         TIMESTAMPTZ,
    verification_notes  TEXT,
    email_sent          BOOLEAN NOT NULL DEFAULT FALSE,
    raw_match_data      JSONB
);

-- ── Notification Logs ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS notification_logs (
    id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    detection_id        UUID REFERENCES detections(id) ON DELETE SET NULL,
    recipient_email     VARCHAR(255) NOT NULL,
    sent_timestamp      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status              VARCHAR(20) NOT NULL DEFAULT 'Pending'
                            CHECK (status IN ('Pending', 'Sent', 'Failed')),
    error_message       TEXT,
    retry_count         INTEGER NOT NULL DEFAULT 0,
    message_id          VARCHAR(255)
);

-- ── Notification Settings ────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS notification_settings (
    id                          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    recipient_emails            TEXT[] NOT NULL DEFAULT '{}',
    minimum_confidence          DECIMAL(4,3) NOT NULL DEFAULT 0.85,
    notify_on_risk_levels       TEXT[] NOT NULL DEFAULT '{High,Critical}',
    rate_limit_minutes          INTEGER NOT NULL DEFAULT 5,
    is_enabled                  BOOLEAN NOT NULL DEFAULT TRUE,
    smtp_host                   VARCHAR(255),
    smtp_port                   INTEGER DEFAULT 587,
    smtp_use_tls                BOOLEAN DEFAULT TRUE,
    from_email                  VARCHAR(255),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Insert default notification settings
INSERT INTO notification_settings (id, recipient_emails, minimum_confidence, notify_on_risk_levels)
VALUES (uuid_generate_v4(), '{}', 0.85, '{High,Critical}')
ON CONFLICT DO NOTHING;

-- ── Performance Indexes ──────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_persons_name_trgm
    ON persons USING gin (name gin_trgm_ops);

CREATE INDEX IF NOT EXISTS idx_persons_risk_level
    ON persons (risk_level);

CREATE INDEX IF NOT EXISTS idx_persons_is_active
    ON persons (is_active);

CREATE INDEX IF NOT EXISTS idx_person_photos_person_id
    ON person_photos (person_id);

CREATE INDEX IF NOT EXISTS idx_rtsp_streams_is_active
    ON rtsp_streams (is_active);

CREATE INDEX IF NOT EXISTS idx_detections_person_id
    ON detections (person_id);

CREATE INDEX IF NOT EXISTS idx_detections_stream_id
    ON detections (stream_id);

CREATE INDEX IF NOT EXISTS idx_detections_timestamp
    ON detections (detection_timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_detections_confidence
    ON detections (confidence_score);

CREATE INDEX IF NOT EXISTS idx_notification_logs_detection_id
    ON notification_logs (detection_id);

CREATE INDEX IF NOT EXISTS idx_notification_logs_status
    ON notification_logs (status);

-- ── Updated At Trigger ───────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION trigger_set_timestamp()
RETURNS TRIGGER AS $$
BEGIN
  NEW.date_updated = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER set_timestamp_persons
    BEFORE UPDATE ON persons
    FOR EACH ROW EXECUTE FUNCTION trigger_set_timestamp();

CREATE TRIGGER set_timestamp_rtsp_streams
    BEFORE UPDATE ON rtsp_streams
    FOR EACH ROW EXECUTE FUNCTION trigger_set_timestamp();
