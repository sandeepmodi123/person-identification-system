# Person Identification System

An enterprise-grade real-time person identification system using RTSP streaming, facial recognition, and multi-service architecture designed for traffic signal cameras to identify persons of interest.

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (for local development)
- Node.js 18+ (for Angular frontend)
- Python 3.10+ (for Python services)

### Run with Docker Compose

```bash
# Clone the repository
git clone https://github.com/sandeepmodi123/person-identification-system.git
cd person-identification-system

# Copy and configure environment variables
cp .env.example .env
# Edit .env with your settings

# Start all services
docker-compose up -d

# Verify all services are running
docker-compose ps
```

### Access Points

| Service | URL |
|---------|-----|
| Angular UI | http://localhost:4200 |
| .NET API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |
| Python Face Recognition | http://localhost:8000 |
| RabbitMQ Management | http://localhost:15672 |

## 📋 System Overview

This system processes live RTSP video streams from traffic signal cameras, performs real-time facial recognition, matches detected faces against a PostgreSQL database of registered persons, and sends email notifications when a match is found.

### Architecture

```
RTSP Cameras → Stream Processor → Face Detector → Face Recognition API
                                                          ↓
                                               .NET Core API ← PostgreSQL
                                                    ↓              ↑
                                           Angular Dashboard    Redis Cache
                                                    ↓
                                          Email Notifications
```

## 📁 Project Structure

```
person-identification-system/
├── src/
│   ├── PersonIdentificationSystem.API/     # .NET Core 8 REST API
│   ├── PersonIdentificationSystem.UI/      # Angular 17 Frontend
│   └── PersonIdentificationSystem.Python/  # Python Services
│       ├── face_recognition_service/       # FastAPI face recognition
│       └── stream_processor/               # RTSP stream processor
├── src/Database/                           # PostgreSQL schema & seeds
├── .github/workflows/                      # CI/CD pipelines
├── docker-compose.yml                      # Multi-service orchestration
├── ARCHITECTURE.md                         # System architecture details
├── API_DOCUMENTATION.md                    # API reference
├── DEPLOYMENT.md                           # Deployment guide
└── USER_GUIDE.md                           # Administrator guide
```

## 🔧 Development Setup

### .NET API

```bash
cd src/PersonIdentificationSystem.API
dotnet restore
dotnet run
```

### Angular UI

```bash
cd src/PersonIdentificationSystem.UI
npm install
ng serve
```

### Python Face Recognition Service

```bash
cd src/PersonIdentificationSystem.Python/face_recognition_service
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

### Python Stream Processor

```bash
cd src/PersonIdentificationSystem.Python/stream_processor
pip install -r requirements.txt
python stream_manager.py
```

## 📖 Documentation

- [Architecture Document](ARCHITECTURE.md)
- [API Documentation](API_DOCUMENTATION.md)
- [Deployment Guide](DEPLOYMENT.md)
- [User Guide](USER_GUIDE.md)

## 🛡️ Security

- All sensitive data encrypted at rest and in transit
- RTSP credentials stored securely
- Comprehensive audit logging
- Input validation on all endpoints

## 📄 License

This project is licensed under the MIT License.
