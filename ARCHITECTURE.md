# System Architecture

## Overview

The Person Identification System is a microservices-based platform that processes RTSP video streams in real time, detects faces, matches them against a database of registered persons, and triggers email notifications on positive matches.

---

## High-Level Architecture

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                               PERSON IDENTIFICATION SYSTEM                             │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│  ┌──────────────┐     ┌───────────────────────────────────────────────────────┐        │
│  │ RTSP Cameras │────▶│           Python Stream Processor                     │        │
│  │ (Traffic     │     │  - stream_manager.py  (RTSP ingestion)                │        │
│  │  Signals)    │     │  - frame_extractor.py (frame sampling)                │        │
│  └──────────────┘     │  - face_detector.py   (YOLO face detection)           │        │
│                       └──────────────────────┬────────────────────────────────┘        │
│                                              │ HTTP POST /api/matching/process-frame   │
│                                              ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────┐       │
│  │                     Python Face Recognition Service (FastAPI)               │       │
│  │  - Receives cropped face images                                             │       │
│  │  - Generates embeddings using FaceNet/ArcFace                               │       │
│  │  - Queries Redis cache for person embeddings                                │       │
│  │  - Returns top-N matches with confidence scores                             │       │
│  └─────────────────────────────────┬───────────────────────────────────────────┘       │
│                                    │ Match results                                     │
│                                    ▼                                                   │
│  ┌─────────────────────────────────────────────────────────────────────────────┐       │
│  │                       .NET Core 8 REST API                                  │       │
│  │  ┌──────────────┐  ┌────────────────┐  ┌─────────────────┐                 │       │
│  │  │ Person       │  │ RTSP Stream    │  │ Detection       │                 │       │
│  │  │ Controller   │  │ Controller     │  │ Controller      │                 │       │
│  │  └──────┬───────┘  └───────┬────────┘  └────────┬────────┘                 │       │
│  │         │                  │                    │                           │       │
│  │  ┌──────▼──────────────────▼────────────────────▼────────┐                 │       │
│  │  │                   Service Layer                        │                 │       │
│  │  │  PersonService │ StreamService │ DetectionService      │                 │       │
│  │  │  MatchingService │ NotificationService                 │                 │       │
│  │  └──────────────────────────┬──────────────────────────-─┘                 │       │
│  │                             │                                               │       │
│  │  ┌──────────────────────────▼──────────────────────────-─┐                 │       │
│  │  │                 Repository Layer                       │                 │       │
│  │  │  PersonRepository │ StreamRepository │ DetectionRepo  │                 │       │
│  │  └──────────────────────────┬──────────────────────────-─┘                 │       │
│  └───────────────────────────────────────────────────────────────────────────-┘       │
│                                │                                                       │
│         ┌──────────────────────┼──────────────────────────┐                           │
│         ▼                      ▼                           ▼                           │
│  ┌─────────────┐       ┌──────────────┐           ┌──────────────┐                    │
│  │ PostgreSQL  │       │  Redis Cache │           │  RabbitMQ    │                    │
│  │ (primary    │       │  (embeddings,│           │  (async      │                    │
│  │  data store)│       │   sessions)  │           │   notifications)                  │
│  └─────────────┘       └──────────────┘           └──────┬───────┘                    │
│                                                          │                             │
│                                                          ▼                             │
│                                                  ┌──────────────┐                     │
│                                                  │ Email Service│                     │
│                                                  │ (SMTP/SendGrid)                    │
│                                                  └──────────────┘                     │
│                                                                                        │
│  ┌─────────────────────────────────────────────────────────────────────────────┐       │
│  │                        Angular 17 Frontend                                  │       │
│  │  ┌──────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌───────────────┐  │       │
│  │  │Dashboard │ │Person Management │ │RTSP Configuration│ │  Detections   │  │       │
│  │  └──────────┘ └──────────────────┘ └──────────────────┘ └───────────────┘  │       │
│  └─────────────────────────────────────────────────────────────────────────────┘       │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Component Details

### 1. Python Stream Processor
**Purpose:** Ingests RTSP streams, samples frames at configurable intervals, detects faces, and dispatches cropped face images to the Face Recognition Service.

| File | Responsibility |
|------|---------------|
| `stream_manager.py` | Manages concurrent RTSP connections, auto-reconnect |
| `frame_extractor.py` | Samples frames at configured FPS, handles buffering |
| `face_detector.py` | YOLO-based face detection, crops face ROIs |
| `config.py` | Configuration loading from environment |
| `logger.py` | Structured logging |
| `exceptions.py` | Domain-specific exceptions |

**Technology:** Python 3.10, OpenCV, Ultralytics YOLO, asyncio

---

### 2. Python Face Recognition Service
**Purpose:** Accepts cropped face images, generates vector embeddings, compares against cached person embeddings, and returns ranked matches.

| File | Responsibility |
|------|---------------|
| `main.py` | FastAPI app setup, lifespan, middleware |
| `routes/matching.py` | `/api/match` endpoint |
| `routes/health.py` | `/health` endpoint |
| `services/embedding_model.py` | FaceNet/ArcFace inference |
| `services/cache_service.py` | Redis read/write for embeddings |
| `services/model_loader.py` | Model loading and warm-up |
| `models/matcher.py` | Cosine similarity, top-N ranking |
| `utils/config.py` | Pydantic settings |
| `utils/logger.py` | Structured logging |

**Technology:** Python 3.10, FastAPI, InsightFace/FaceNet, Redis, NumPy

---

### 3. .NET Core 8 REST API
**Purpose:** Central business logic layer — manages person profiles, stream configuration, persists detections, triggers notifications.

| Layer | Files |
|-------|-------|
| Controllers | PersonController, RTSPStreamController, DetectionController, NotificationController, HealthController |
| Services | PersonService, StreamService, DetectionService, MatchingService, NotificationService |
| Repositories | PersonRepository, StreamRepository, DetectionRepository, NotificationLogRepository |
| Entities | Person, PersonPhoto, RTSPStream, Detection, NotificationLog |
| DTOs | PersonDto, PersonPhotoDto, RTSPStreamDto, DetectionDto, NotificationSettingsDto |
| Middleware | ExceptionHandlingMiddleware, PerformanceMonitoringMiddleware, LoggingMiddleware |
| Infrastructure | PythonFaceRecognitionClient, EmailNotificationService |

**Technology:** .NET 8, ASP.NET Core, Entity Framework Core, Npgsql, MediatR, Serilog

---

### 4. PostgreSQL Database
**Purpose:** Primary persistent data store for all system entities.

**Tables:**
- `persons` — Profile information, risk level, status
- `person_photos` — Associated photos with quality scores
- `rtsp_streams` — Camera URLs, locations, active status
- `detections` — Match events with confidence scores, frame snapshots
- `notification_logs` — Delivery audit trail

---

### 5. Redis Cache
**Purpose:** Caches face embeddings for fast similarity lookup. Stores embeddings as JSON with person_id as key.

---

### 6. RabbitMQ
**Purpose:** Asynchronous notification queue. Detection events are published; the notification consumer processes emails with retry logic.

---

### 7. Angular 17 Frontend
**Purpose:** Administrator web UI for managing persons, streams, viewing detections, and configuring notifications.

**Feature Modules:**
- `DashboardModule` — Live detection feed, summary metrics
- `PersonManagementModule` — CRUD for persons and photos
- `RtspConfigurationModule` — Stream management
- `DetectionsModule` — Detection history, filtering, verification
- `NotificationsModule` — Email settings, delivery logs

---

## Data Flow: Detection Event

```
1. Stream Processor connects to RTSP URL
2. Frame extractor samples frame every N seconds
3. YOLO detector finds face bounding boxes in frame
4. Face ROI is cropped and base64-encoded
5. HTTP POST to Face Recognition Service /api/match
6. Service generates embedding → queries Redis
7. Cosine similarity computed against all person embeddings
8. Top match returned (if confidence ≥ threshold)
9. Match result POSTed to .NET API /api/detections
10. API persists Detection record to PostgreSQL
11. If email_notifications enabled → publishes to RabbitMQ
12. Notification consumer sends email via SMTP/SendGrid
13. NotificationLog record created
14. Angular dashboard receives real-time update (polling or SignalR)
```

---

## Security Architecture

- **Transport:** All inter-service communication over TLS in production
- **Secrets:** Environment variables injected at runtime; never in code
- **Database:** Parameterized queries via EF Core; no raw SQL with user input
- **File Uploads:** File type and size validation; stored outside web root
- **RTSP URLs:** Stored encrypted in database; decrypted only in Stream Processor
- **Audit Logging:** All detection matches and verifications logged with timestamp

---

## Scalability Considerations

- Stream Processor scales horizontally; each instance handles N streams
- Face Recognition Service is stateless; scale behind load balancer
- PostgreSQL with read replicas for heavy query load
- Redis Cluster for embedding cache in production
- RabbitMQ with consumer groups for parallel notification processing
