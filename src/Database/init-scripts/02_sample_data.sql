-- ============================================================
-- Sample Data for Development/Testing
-- ============================================================

-- Sample Persons
INSERT INTO persons (id, name, description, risk_level, is_active) VALUES
  ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'John Doe', 'Sample high-risk individual for testing', 'High', TRUE),
  ('b2c3d4e5-f6a7-8901-bcde-f12345678901', 'Jane Smith', 'Sample medium-risk individual', 'Medium', TRUE),
  ('c3d4e5f6-a7b8-9012-cdef-012345678902', 'Robert Johnson', 'Sample low-risk individual', 'Low', TRUE),
  ('d4e5f6a7-b8c9-0123-def0-123456789013', 'Emily Davis', 'Critical alert - do not approach alone', 'Critical', TRUE)
ON CONFLICT (id) DO NOTHING;

-- Sample RTSP Streams
INSERT INTO rtsp_streams (id, camera_name, camera_location, rtsp_url, frame_interval_seconds, is_active, status) VALUES
  ('e5f6a7b8-c9d0-1234-ef01-234567890124', 'Main Junction Cam 1', 'MG Road & Brigade Road Junction', 'rtsp://demo:demo@demo.camera1:554/stream', 5, TRUE, 'Unknown'),
  ('f6a7b8c9-d0e1-2345-f012-345678901235', 'Traffic Signal East', 'East Avenue - Signal 3', 'rtsp://demo:demo@demo.camera2:554/stream', 10, TRUE, 'Unknown'),
  ('a7b8c9d0-e1f2-3456-0123-456789012346', 'North Gate Camera', 'Highway 17 North Gate', 'rtsp://demo:demo@demo.camera3:554/stream', 5, FALSE, 'Unknown')
ON CONFLICT (id) DO NOTHING;
