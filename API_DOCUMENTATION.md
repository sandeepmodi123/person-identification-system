# API Documentation

Base URL: `http://localhost:5000/api`

All endpoints accept and return `application/json` unless noted. Authentication (JWT Bearer) should be added before production deployment.

---

## Person Management

### Create Person
`POST /persons`

**Request Body:**
```json
{
  "name": "John Doe",
  "description": "Wanted for armed robbery",
  "riskLevel": "High",
  "isActive": true
}
```

**Response `201`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "John Doe",
  "description": "Wanted for armed robbery",
  "riskLevel": "High",
  "isActive": true,
  "dateAdded": "2024-01-15T10:30:00Z",
  "photos": []
}
```

---

### List Persons
`GET /persons?page=1&pageSize=20&search=john&riskLevel=High&isActive=true`

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| page | int | Page number (default: 1) |
| pageSize | int | Items per page (default: 20, max: 100) |
| search | string | Search by name |
| riskLevel | string | Filter: Low, Medium, High, Critical |
| isActive | bool | Filter by active status |

**Response `200`:**
```json
{
  "items": [ /* PersonDto array */ ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

---

### Get Person
`GET /persons/{id}`

**Response `200`:** Full `PersonDto` with `photos` array.

---

### Update Person
`PUT /persons/{id}`

**Request Body:** Same as Create Person (all fields optional).

**Response `200`:** Updated `PersonDto`.

---

### Delete Person
`DELETE /persons/{id}`

**Response `204`:** No content.

---

### Upload Person Photo
`POST /persons/{id}/photos`

**Content-Type:** `multipart/form-data`

**Form Fields:**
| Field | Type | Description |
|-------|------|-------------|
| photo | file | JPEG/PNG image, max 10 MB |
| isPrimary | bool | Set as primary photo |

**Response `201`:**
```json
{
  "id": "uuid",
  "personId": "uuid",
  "photoUrl": "/uploads/persons/uuid/photo.jpg",
  "qualityScore": 0.92,
  "uploadDate": "2024-01-15T10:35:00Z",
  "isPrimary": true
}
```

---

### Get Person Photos
`GET /persons/{id}/photos`

**Response `200`:** Array of `PersonPhotoDto`.

---

### Delete Person Photo
`DELETE /persons/{id}/photos/{photoId}`

**Response `204`:** No content.

---

### Bulk Import Persons
`POST /persons/bulk-import`

**Content-Type:** `multipart/form-data`

**Form Fields:**
| Field | Type | Description |
|-------|------|-------------|
| file | file | CSV file with headers: name,description,riskLevel |

**Response `200`:**
```json
{
  "imported": 45,
  "failed": 2,
  "errors": [
    { "row": 12, "reason": "Invalid riskLevel value" }
  ]
}
```

---

## RTSP Stream Management

### Add RTSP Stream
`POST /rtsp-streams`

**Request Body:**
```json
{
  "cameraName": "Main Junction Camera",
  "cameraLocation": "MG Road & Brigade Road Junction",
  "rtspUrl": "rtsp://user:pass@192.168.1.100:554/stream1",
  "frameIntervalSeconds": 5,
  "isActive": true
}
```

**Response `201`:** `RTSPStreamDto`

---

### List RTSP Streams
`GET /rtsp-streams`

**Response `200`:** Array of `RTSPStreamDto`.

---

### Get RTSP Stream
`GET /rtsp-streams/{id}`

**Response `200`:** `RTSPStreamDto`

---

### Update RTSP Stream
`PUT /rtsp-streams/{id}`

**Request Body:** Same as Add RTSP Stream.

**Response `200`:** Updated `RTSPStreamDto`.

---

### Delete RTSP Stream
`DELETE /rtsp-streams/{id}`

**Response `204`:** No content.

---

### Test Stream Connection
`POST /rtsp-streams/{id}/test-connection`

**Response `200`:**
```json
{
  "streamId": "uuid",
  "isReachable": true,
  "latencyMs": 120,
  "testedAt": "2024-01-15T10:40:00Z",
  "errorMessage": null
}
```

---

## Detections

### List Detections
`GET /detections?page=1&pageSize=20&streamId=uuid&personId=uuid&fromDate=2024-01-01&toDate=2024-01-31&minConfidence=0.85`

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| page | int | Page number |
| pageSize | int | Items per page |
| streamId | guid | Filter by stream |
| personId | guid | Filter by person |
| fromDate | datetime | Start date filter |
| toDate | datetime | End date filter |
| minConfidence | float | Minimum confidence score (0-1) |
| isVerified | bool | Filter verified/unverified |

**Response `200`:** Paginated list of `DetectionDto`.

---

### Get Detection
`GET /detections/{id}`

**Response `200`:**
```json
{
  "id": "uuid",
  "streamId": "uuid",
  "cameraName": "Main Junction Camera",
  "personId": "uuid",
  "personName": "John Doe",
  "riskLevel": "High",
  "confidenceScore": 0.94,
  "detectionTimestamp": "2024-01-15T14:22:33Z",
  "frameImageUrl": "/uploads/detections/uuid/frame.jpg",
  "isVerified": false,
  "verificationStatus": null,
  "emailSent": true
}
```

---

### Verify Detection
`POST /detections/{id}/verify`

**Request Body:**
```json
{
  "status": "TruePositive",
  "notes": "Confirmed by officer badge #1234"
}
```

`status` values: `TruePositive`, `FalsePositive`

**Response `200`:** Updated `DetectionDto`.

---

### Process Frame (Called by Stream Processor)
`POST /matching/process-frame`

**Request Body:**
```json
{
  "streamId": "uuid",
  "frameBase64": "base64-encoded-image",
  "capturedAt": "2024-01-15T14:22:33Z"
}
```

**Response `200`:**
```json
{
  "matchFound": true,
  "detectionId": "uuid",
  "personId": "uuid",
  "personName": "John Doe",
  "confidenceScore": 0.94,
  "notificationSent": true
}
```

---

## Notifications

### Get Notification Settings
`GET /notifications/settings`

**Response `200`:**
```json
{
  "id": "uuid",
  "recipientEmails": ["officer@dept.gov", "supervisor@dept.gov"],
  "minimumConfidenceThreshold": 0.85,
  "notifyOnRiskLevels": ["High", "Critical"],
  "rateLimitMinutes": 5,
  "isEnabled": true,
  "smtpHost": "smtp.example.com",
  "smtpPort": 587,
  "fromEmail": "alerts@system.com"
}
```

---

### Update Notification Settings
`PUT /notifications/settings`

**Request Body:** Same structure as GET response (omit `id`).

**Response `200`:** Updated settings.

---

### Get Notification Logs
`GET /notifications/logs?page=1&pageSize=20&status=Sent`

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| page | int | Page number |
| pageSize | int | Items per page |
| status | string | Filter: Sent, Failed, Pending |
| detectionId | guid | Filter by detection |

**Response `200`:** Paginated list of `NotificationLogDto`.

---

## Health Check

### Health Status
`GET /health`

**Response `200`:**
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "timestamp": "2024-01-15T14:22:33Z",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "pythonService": "Healthy",
    "rabbitMq": "Healthy"
  }
}
```

---

## Data Models

### PersonDto
```typescript
{
  id: string;           // GUID
  name: string;
  description: string;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  isActive: boolean;
  dateAdded: string;    // ISO 8601
  photos: PersonPhotoDto[];
}
```

### PersonPhotoDto
```typescript
{
  id: string;
  personId: string;
  photoUrl: string;
  qualityScore: number; // 0-1
  uploadDate: string;
  isPrimary: boolean;
}
```

### RTSPStreamDto
```typescript
{
  id: string;
  cameraName: string;
  cameraLocation: string;
  rtspUrl: string;
  frameIntervalSeconds: number;
  isActive: boolean;
  status: 'Online' | 'Offline' | 'Error' | 'Unknown';
  lastChecked: string;
}
```

### DetectionDto
```typescript
{
  id: string;
  streamId: string;
  cameraName: string;
  personId: string;
  personName: string;
  riskLevel: string;
  confidenceScore: number;
  detectionTimestamp: string;
  frameImageUrl: string;
  isVerified: boolean;
  verificationStatus: 'TruePositive' | 'FalsePositive' | null;
  emailSent: boolean;
}
```

### NotificationLogDto
```typescript
{
  id: string;
  detectionId: string;
  recipientEmail: string;
  sentTimestamp: string;
  status: 'Sent' | 'Failed' | 'Pending';
  errorMessage: string | null;
}
```

---

## Error Responses

All errors follow this structure:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Detailed error message",
  "traceId": "00-abc123-def456-00"
}
```

| Status | Meaning |
|--------|---------|
| 400 | Bad Request — validation error |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Resource not found |
| 409 | Conflict (duplicate) |
| 422 | Unprocessable Entity |
| 500 | Internal Server Error |
