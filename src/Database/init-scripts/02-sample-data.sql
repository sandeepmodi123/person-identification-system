-- =============================================================================
-- Sample Data Seed Script
-- Run this after 01-schema.sql to populate the database with test data
-- =============================================================================

-- Sample persons
INSERT INTO persons (id, name, description, risk_level, is_active) VALUES
    ('a1b2c3d4-0001-0001-0001-000000000001', 'John Doe',    'Wanted for robbery. Last seen downtown.', 'High',     TRUE),
    ('a1b2c3d4-0002-0002-0002-000000000002', 'Jane Smith',  'Missing person case. Cognitive impairment.', 'Medium', TRUE),
    ('a1b2c3d4-0003-0003-0003-000000000003', 'Raj Kumar',   'Armed and dangerous. Approach with caution.', 'Critical', TRUE),
    ('a1b2c3d4-0004-0004-0004-000000000004', 'Ali Hassan',  'Petty theft suspect.', 'Low',       TRUE),
    ('a1b2c3d4-0005-0005-0005-000000000005', 'Maria Lopez', 'Fraud suspect - white collar.', 'Medium', FALSE)
ON CONFLICT DO NOTHING;

-- Sample RTSP streams
INSERT INTO rtsp_streams (id, camera_name, camera_location, rtsp_url, frame_interval_seconds, is_active, status) VALUES
    ('b1b2c3d4-0001-0001-0001-000000000001', 'MG Road Junction', 'MG Road & Brigade Rd, Bangalore', 'rtsp://192.168.1.101:554/stream1', 5, TRUE, 'Unknown'),
    ('b1b2c3d4-0002-0002-0002-000000000002', 'Airport Entry', 'Kempegowda Int Airport, T2', 'rtsp://192.168.1.102:554/stream1', 10, TRUE, 'Unknown'),
    ('b1b2c3d4-0003-0003-0003-000000000003', 'Bus Terminal', 'Majestic Bus Station, Gate 3', 'rtsp://192.168.1.103:554/stream1', 5, FALSE, 'Unknown')
ON CONFLICT DO NOTHING;

-- Sample notification settings (update the default row)
UPDATE notification_settings SET
    recipient_emails = ARRAY['admin@yoursystem.com', 'police@yoursystem.com'],
    minimum_confidence = 0.85,
    notify_on_risk_levels = ARRAY['High', 'Critical'],
    rate_limit_minutes = 5,
    is_enabled = TRUE
WHERE id = (SELECT id FROM notification_settings LIMIT 1);

SELECT 'Sample data loaded successfully.' AS message;
