# Deployment Guide

## Prerequisites

- Docker Engine 24+ and Docker Compose 2.20+
- 8 GB RAM minimum (16 GB recommended)
- 20 GB available disk space
- Ports 4200, 5000, 5432, 6379, 5672, 15672, 8000, 8001 available

---

## 1. Local Development with Docker Compose

### 1.1 Configure Environment

```bash
cp .env.example .env
```

Edit `.env` and set all required values:

```bash
# Database
POSTGRES_PASSWORD=your_secure_password

# Email
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=your@email.com
SMTP_PASSWORD=your_app_password
FROM_EMAIL=alerts@yoursystem.com

# Python Face Recognition
FACE_RECOGNITION_MODEL=arcface   # arcface or facenet
CONFIDENCE_THRESHOLD=0.85
```

### 1.2 Start Services

```bash
# Build and start all services
docker-compose up -d --build

# Follow logs
docker-compose logs -f

# Check health
docker-compose ps
```

### 1.3 Initialize Database

The database is automatically initialized from `src/Database/init-scripts/01_schema.sql` on first start.

To manually re-initialize:

```bash
docker-compose exec postgres psql -U personid_user -d personid_db -f /docker-entrypoint-initdb.d/01_schema.sql
```

### 1.4 Stop Services

```bash
docker-compose down          # Stop containers
docker-compose down -v       # Stop and remove volumes (clears data)
```

---

## 2. Production Deployment

### 2.1 Server Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 4 cores | 8+ cores |
| RAM | 8 GB | 16 GB |
| Storage | 50 GB SSD | 200 GB SSD |
| OS | Ubuntu 22.04 LTS | Ubuntu 22.04 LTS |

### 2.2 Environment Variables for Production

Set these in your deployment platform's secrets manager:

```bash
ASPNETCORE_ENVIRONMENT=Production
POSTGRES_HOST=your-db-host
POSTGRES_PASSWORD=<strong-password>
REDIS_CONNECTION=your-redis:6379,password=<password>
RABBITMQ_PASSWORD=<strong-password>
SMTP_PASSWORD=<email-password>
PYTHON_SERVICE_URL=http://face-recognition:8000
```

### 2.3 Docker Compose Production Override

```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

### 2.4 Nginx Reverse Proxy (Recommended)

```nginx
server {
    listen 80;
    server_name yourdomain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;

    # Angular Frontend
    location / {
        proxy_pass http://localhost:4200;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
    }

    # .NET API
    location /api/ {
        proxy_pass http://localhost:5000/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

---

## 3. Database Migrations

```bash
# Apply migrations (inside API container or locally with dotnet ef)
docker-compose exec api dotnet ef database update

# Create new migration
cd src/PersonIdentificationSystem.API
dotnet ef migrations add MigrationName
```

---

## 4. Monitoring & Logs

```bash
# View all logs
docker-compose logs -f

# View specific service logs
docker-compose logs -f api
docker-compose logs -f face-recognition
docker-compose logs -f stream-processor

# API health endpoint
curl http://localhost:5000/health

# Python service health
curl http://localhost:8000/health
```

---

## 5. Backup & Restore

### Backup PostgreSQL

```bash
docker-compose exec postgres pg_dump -U personid_user personid_db > backup_$(date +%Y%m%d).sql
```

### Restore PostgreSQL

```bash
cat backup_20240115.sql | docker-compose exec -T postgres psql -U personid_user personid_db
```

---

## 6. Scaling

### Scale Face Recognition Workers

```bash
docker-compose up -d --scale face-recognition=3
```

### Scale Stream Processor

```bash
docker-compose up -d --scale stream-processor=2
```

Each stream-processor instance handles the streams assigned to it via environment variable `STREAM_IDS`.

---

## 7. CI/CD Pipeline

The GitHub Actions pipeline (`.github/workflows/ci-cd.yml`) automatically:

1. **On Pull Request:**
   - Builds all services
   - Runs .NET unit tests
   - Runs Python tests
   - Lints Angular code

2. **On merge to `main`:**
   - All PR checks
   - Builds and pushes Docker images to registry
   - Deploys to staging environment

Configure these GitHub Secrets for the pipeline:
- `DOCKER_REGISTRY_URL`
- `DOCKER_USERNAME`
- `DOCKER_PASSWORD`
- `DEPLOY_SSH_KEY`
- `DEPLOY_HOST`
