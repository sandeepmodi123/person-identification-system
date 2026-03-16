# Administrator User Guide

## Getting Started

### Accessing the System

Open a web browser and navigate to `http://your-server:4200`. Log in with the credentials provided by your system administrator.

---

## Dashboard

The Dashboard provides a real-time overview of the system.

**Key sections:**
- **Live Detection Feed** — Shows the most recent detections as they occur
- **Active Streams** — Count of currently active RTSP camera streams
- **Today's Detections** — Number of person matches today
- **High-Risk Alerts** — Detections flagged as high or critical risk

---

## Person Management

### Adding a New Person

1. Navigate to **Person Management** → **Add Person**
2. Fill in the required fields:
   - **Name** (required)
   - **Description** — Case details or notes
   - **Risk Level** — Low / Medium / High / Critical
3. Click **Save Person**
4. You will be redirected to the person's detail page to upload photos

### Uploading Photos

Good photos are critical for accurate recognition. Guidelines:
- Use **front-facing**, well-lit photos
- Minimum resolution: **400×400 pixels**
- File format: **JPEG or PNG**, max **10 MB**
- Upload **multiple photos** from different angles for best results

Steps:
1. Open the person's detail page
2. Click **Upload Photo**
3. Select the photo file
4. Optionally check **Set as Primary** for the main identification photo
5. Click **Upload**

The system will automatically score photo quality. Photos with a score below 0.5 will show a warning.

### Editing a Person

1. Navigate to **Person Management**
2. Search for the person by name or ID
3. Click **Edit** (pencil icon)
4. Make changes and click **Save**

### Deactivating a Person

Deactivating removes the person from live matching without deleting their record.

1. Open the person's detail page
2. Toggle **Active Status** to OFF
3. Confirm the action

### Bulk Import

To add many persons at once:

1. Prepare a CSV file with columns: `name,description,riskLevel`
   ```
   name,description,riskLevel
   John Doe,Wanted for robbery,High
   Jane Smith,Missing person,Medium
   ```
2. Navigate to **Person Management** → **Bulk Import**
3. Select your CSV file and click **Import**
4. Review the import summary (imported count, any errors)
5. Upload photos for each imported person

---

## RTSP Stream Configuration

### Adding a Camera Stream

1. Navigate to **RTSP Configuration** → **Add Stream**
2. Fill in:
   - **Camera Name** — Descriptive label (e.g., "MG Road Junction")
   - **Camera Location** — Physical location description
   - **RTSP URL** — Full RTSP URL (e.g., `rtsp://user:pass@192.168.1.100:554/stream1`)
   - **Frame Interval** — How often to capture a frame for analysis (seconds)
3. Click **Test Connection** to verify the camera is reachable
4. Click **Save Stream**

### Managing Streams

| Action | Description |
|--------|-------------|
| **Enable/Disable** | Toggle processing for a stream without deleting it |
| **Test Connection** | Verify the RTSP URL is reachable |
| **Edit** | Update stream URL or settings |
| **Delete** | Permanently remove the stream |

### Stream Status Indicators

| Status | Meaning |
|--------|---------|
| 🟢 Online | Stream is active and being processed |
| 🔴 Offline | Stream cannot be reached |
| 🟡 Error | Stream connected but encountering errors |
| ⚪ Unknown | Status not yet checked |

---

## Detections

### Viewing Detections

Navigate to **Detections** to see all match events. Use filters to narrow results:

- **Date Range** — Filter by detection date
- **Camera** — Filter by specific camera
- **Person** — Filter by specific person
- **Minimum Confidence** — Only show matches above a threshold
- **Risk Level** — Filter by person risk level
- **Verified Status** — Show only verified or unverified detections

### Understanding Confidence Scores

| Score | Meaning |
|-------|---------|
| 95–100% | Very high confidence — strong match |
| 85–94% | High confidence — likely match |
| 75–84% | Medium confidence — possible match |
| Below 75% | Low confidence — review recommended |

The system only triggers notifications for matches above the configured threshold (default: 85%).

### Verifying a Detection

Manual verification helps improve system accuracy.

1. Click on a detection to open its detail view
2. Review the captured frame and matched person's photos
3. Click **True Positive** (correct match) or **False Positive** (incorrect match)
4. Add optional notes
5. Click **Confirm**

---

## Notifications

### Configuring Email Recipients

1. Navigate to **Notifications** → **Settings**
2. Add email addresses in the **Recipients** field (one per line)
3. Click **Save**

### Notification Rules

Configure when notifications are sent:

| Setting | Description |
|---------|-------------|
| **Minimum Confidence** | Only send for matches above this score |
| **Risk Levels** | Only notify for selected risk levels |
| **Rate Limit** | Minimum minutes between notifications for the same person |
| **Enable/Disable** | Globally toggle all notifications |

### Viewing Notification History

Navigate to **Notifications** → **Logs** to see delivery status for all sent alerts.

| Status | Meaning |
|--------|---------|
| ✅ Sent | Email delivered successfully |
| ❌ Failed | Delivery failed (see error message) |
| ⏳ Pending | Queued, not yet sent |

---

## Troubleshooting

### Camera Stream Not Connecting

1. Verify the RTSP URL is correct (username/password, IP, port, path)
2. Ensure the camera is powered on and network-accessible
3. Test the URL in VLC Media Player: File → Open Network Stream
4. Check if the server running the Stream Processor can reach the camera IP

### No Detections Despite Persons Being Visible

1. Check that the person has at least one uploaded photo
2. Verify the person's Active Status is ON
3. Check the confidence threshold — lower it if needed
4. Ensure photos are clear, front-facing, and well-lit
5. Check the Stream Processor logs for face detection errors

### Email Notifications Not Arriving

1. Check **Notifications** → **Logs** for error messages
2. Verify SMTP settings are correct
3. Check spam/junk folders
4. Ensure recipient emails are correctly configured

### System Performance Issues

- Reduce the number of active RTSP streams
- Increase the frame interval (less frequent analysis)
- Contact your system administrator to scale the Face Recognition service

---

## Support

For technical support, contact your system administrator or open an issue at the project repository.
