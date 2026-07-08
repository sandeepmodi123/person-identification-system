# Deployment Guide

End-to-end steps to deploy the Person Identification System to Azure.

## Architecture

| Component | Azure service | Purpose |
|---|---|---|
| `PersonIdentificationSystem.UI` (Angular) | **Azure Static Web Apps (Free)** | Web frontend |
| `PersonIdentificationSystem.API` (.NET) | **Azure App Service – Linux** | REST API, SignalR hub |
| `face_recognition_service` (FastAPI) | **Azure Container Apps – Consumption (scale to 0)** | Thin proxy to CompreFace |
| `stream_processor` (Python) | **Azure Container Apps – min 1 replica** | RTSP pull + MJPEG live feed |
| CompreFace | Existing VM at `http://20.219.170.37:8000/` | Face brain (recognition) |
| PostgreSQL | Azure Database for PostgreSQL – Flexible Server | App data |

Expected monthly cost: **~$15–20** (with both Container Apps running). Scale `stream_processor` to 0 when not demoing to drop to ~$2–5.

---

## Live URLs

| Surface | URL |
|---|---|
| UI | https://red-mud-05244a700.7.azurestaticapps.net |
| API | https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net |
| CompreFace | http://20.219.170.37:8000/ |

---

## 0. Prerequisites

```powershell
# Install / update Azure CLI
winget install -e --id Microsoft.AzureCLI   # or: choco install azure-cli

# Container Apps extension
az extension add --name containerapp --upgrade

# Sign in & pick subscription
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID>"

# Verify the right subscription is active
az account show --query "{name:name, id:id}"
```

### Register required resource providers (one-time per subscription)

If you see `MissingSubscriptionRegistration`, register the providers:

```powershell
az provider register --namespace Microsoft.ContainerRegistry
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
az provider register --namespace Microsoft.Web
az provider register --namespace Microsoft.DBforPostgreSQL

# Wait until each returns "Registered" (usually 1–3 min)
az provider show -n Microsoft.ContainerRegistry --query registrationState
az provider show -n Microsoft.App --query registrationState
az provider show -n Microsoft.OperationalInsights --query registrationState
```

> If `az provider register` fails with `AuthorizationFailed`, your account is not Owner/Contributor on the subscription. Ask an admin to run the commands above or grant you Contributor.

---

## 1. Shared variables

```powershell
$RG          = "rg-personid"
$LOC         = "centralindia"
$ACR         = "personidacr$(Get-Random -Maximum 9999)"   # must be globally unique
$ENV_NAME    = "cae-personid"
$FACE_APP    = "face-recognition"
$STREAM_APP  = "stream-processor"

$UI_ORIGIN   = "https://red-mud-05244a700.7.azurestaticapps.net"
$API_BASE    = "https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net"
$COMPREFACE_URL = "http://98.83.137.214:8000/"
$COMPREFACE_KEY = "df7026b0-adf6-4e6d-9334-60b0a1226bfc"
```

---

## 2. Resource group + Container Registry + Container Apps environment

```powershell
az group create -n $RG -l $LOC

# ACR Basic is the cheapest tier (~$5/mo). Skip and use Docker Hub if you prefer.
az acr create -n $ACR -g $RG --sku Basic --admin-enabled true

# Disable logs to save ~$3/mo while in POC mode
az containerapp env create `
  -n $ENV_NAME -g $RG -l $LOC `
  --logs-destination none
```

---

## 3. Build & push images

`az acr build` builds inside Azure — no local Docker required.

```powershell
$ACR_LOGIN = az acr show -n $ACR --query loginServer -o tsv

# face_recognition_service
cd src\PersonIdentificationSystem.Python\face_recognition_service
az acr build -t "$ACR_LOGIN/face-recognition:v1" -r $ACR .

# stream_processor
cd ..\stream_processor
az acr build -t "$ACR_LOGIN/stream-processor:v1" -r $ACR .

cd ..\..\..\..\
```

Fetch ACR pull credentials:

```powershell
$ACR_USER = az acr credential show -n $ACR --query username -o tsv
$ACR_PASS = az acr credential show -n $ACR --query "passwords[0].value" -o tsv
```

---

## 4. Deploy `face_recognition_service` (scale-to-zero)

```powershell
az containerapp create `
  -n $FACE_APP -g $RG `
  --environment $ENV_NAME `
  --image "$ACR_LOGIN/face-recognition:v1" `
  --registry-server $ACR_LOGIN `
  --registry-username $ACR_USER `
  --registry-password $ACR_PASS `
  --target-port 8000 `
  --ingress external `
  --min-replicas 0 `
  --max-replicas 3 `
  --cpu 0.25 --memory 0.5Gi `
  --env-vars `
    COMPREFACE_URL=$COMPREFACE_URL `
    COMPREFACE_API_KEY=$COMPREFACE_KEY `
    CONFIDENCE_THRESHOLD=0.90 `
    MIN_FACE_SIZE_PX=60 `
    DEDUP_COOLDOWN_SECONDS=30 `
    DOTNET_API_URL="$API_BASE/api"

$FACE_URL = az containerapp show -n $FACE_APP -g $RG --query "properties.configuration.ingress.fqdn" -o tsv
"Face service: https://$FACE_URL"
```

First call will have a ~3–5 s cold start. Subsequent calls run warm until idle timeout.

---

## 5. Deploy `stream_processor` (always-on, MJPEG live feed)

```powershell
az containerapp create `
  -n $STREAM_APP -g $RG `
  --environment $ENV_NAME `
  --image "$ACR_LOGIN/stream-processor:v1" `
  --registry-server $ACR_LOGIN `
  --registry-username $ACR_USER `
  --registry-password $ACR_PASS `
  --target-port 8085 `
  --ingress external `
  --transport http `
  --min-replicas 1 `
  --max-replicas 1 `
  --cpu 0.5 --memory 1.0Gi `
  --env-vars `
    API_BASE_URL="$API_BASE/api" `
    FACE_SERVICE_URL="https://$FACE_URL" `
    TARGET_FPS=4 `
    LOG_LEVEL=INFO

$STREAM_URL = az containerapp show -n $STREAM_APP -g $RG --query "properties.configuration.ingress.fqdn" -o tsv
"Stream processor: https://$STREAM_URL"
```

> **Cameras must be reachable from Azure.** If your RTSP cameras live on a private LAN, ACA cannot reach them. Either expose them publicly behind an NVR, set up a site-to-site VPN, or run `stream_processor` on-prem and only host `face_recognition_service` in Azure.

> Confirm the MJPEG listen port in `mjpeg_server.py`. If it's different from `8085`, change `--target-port` to match. ACA exposes a single external port per app.

---

## 6. Wire everything together

### 6a. .NET API → point at the new face service

```powershell
az webapp config appsettings set `
  -g <YOUR_API_RG> -n citycopapi `
  --settings `
    "PythonService__BaseUrl=https://$FACE_URL" `
    "Cors__AllowedOrigins__0=http://localhost:4200" `
    "Cors__AllowedOrigins__1=$UI_ORIGIN"

# Enable WebSockets so SignalR doesn't fall back to long-polling
az webapp config set -g <YOUR_API_RG> -n citycopapi --web-sockets-enabled true
```

### 6b. Angular UI → set production URLs

Edit `src/PersonIdentificationSystem.UI/src/environments/environment.prod.ts`:

```ts
export const environment = {
  production: true,
  apiBaseUrl:    'https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net/api',
  signalrHubUrl: 'https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net/hubs/detections',
  mjpegBaseUrl:  'https://<STREAM_URL_FROM_STEP_5>',
};
```

Then build and deploy:

```powershell
cd src\PersonIdentificationSystem.UI
ng build --configuration production
# Push dist/ to Azure Static Web Apps (GitHub Action recommended)
```

---

## 7. CompreFace one-time setup

After all services are up, enroll the existing people from the .NET DB into CompreFace:

```powershell
# Triggers /api/sync-embeddings on the face service
curl.exe -X POST "https://$FACE_URL/api/sync-embeddings"
```

---

## 8. Smoke tests

```powershell
# Health
curl.exe "https://$FACE_URL/health"
# Expected: {"status":"healthy","compreface_reachable":true}

# UI
start "" "$UI_ORIGIN"

# API
curl.exe "$API_BASE/api/detections?page=1&pageSize=1"
```

---

## 9. Updating images (every deploy)

```powershell
# Rebuild
az acr build -t "$ACR_LOGIN/face-recognition:v2" -r $ACR `
  src\PersonIdentificationSystem.Python\face_recognition_service

# Roll the Container App
az containerapp update -n $FACE_APP -g $RG --image "$ACR_LOGIN/face-recognition:v2"
```

Same pattern for `stream-processor`.

---

## 10. Cost controls

| Action | Savings |
|---|---|
| `--min-replicas 0` on `face-recognition` | Only pay when calls happen |
| Stop `stream-processor` when not demoing: `az containerapp update -n $STREAM_APP -g $RG --min-replicas 0 --max-replicas 0` | ~$10/mo |
| `--logs-destination none` on ACA env | ~$3/mo |
| Switch ACR to Docker Hub (free public registry) | ~$5/mo |
| Use 1-year reservation on Postgres / VM | ~30% off |

To tear down everything:

```powershell
az group delete -n $RG --yes --no-wait
```

---

## 11. Cheapest alternative: single B1s VM (~$5–8/mo)

If you'd rather manage one VM than two Container Apps:

```powershell
az group create -n rg-personid -l centralindia

az vm create -g rg-personid -n vm-personid `
  --image Ubuntu2204 --size Standard_B1s `
  --admin-username azureuser --generate-ssh-keys `
  --public-ip-sku Standard `
  --nsg-rule SSH

az vm open-port -g rg-personid -n vm-personid --port 8000 --priority 1010
az vm open-port -g rg-personid -n vm-personid --port 8085 --priority 1020
```

Then SSH in and run both services with `docker compose` using the existing Dockerfiles.

Trade-offs: cheapest, but you patch the VM, manage TLS (use Caddy / Let's Encrypt), and have no auto-scale.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `MissingSubscriptionRegistration` | Run the `az provider register ...` commands in §0 |
| `AuthorizationFailed` on provider register | Need Contributor on the subscription |
| Container App stuck `Activating` | `az containerapp logs show -n $FACE_APP -g $RG --follow` |
| CORS error in browser | Confirm App Setting `Cors__AllowedOrigins__1` equals the SWA URL exactly (no trailing slash) and restart the App Service |
| MJPEG blocked in browser | Browser blocks `http://` MJPEG on an `https://` page — ACA gives HTTPS automatically; make sure `mjpegBaseUrl` uses `https://` |
| `face_recognition_service` returns `compreface_reachable: false` | Confirm the CompreFace VM is up and outbound to `20.219.170.37:8000` is open (ACA has unrestricted egress by default) |
| Slow first match call | Cold start. Set `--min-replicas 1` if you need always-warm (adds ~$10/mo) |
