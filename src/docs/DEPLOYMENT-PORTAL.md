# Deploy from the Azure Portal (no CLI)

> Note: this guide reflects the legacy two-container Python deployment. The repository now uses a merged Python runtime; see [PROJECT_RUN_AND_DATA_FLOW.md](../../PROJECT_RUN_AND_DATA_FLOW.md) for current runtime wiring.

Step-by-step instructions to deploy the two Python services **entirely from the Azure Portal**, no `az` CLI needed. Assumes the .NET API (`citycopapi`) and the Static Web App (`red-mud-05244a700`) are already running in Azure.

Total time: ~30–40 min the first time.

---

## What you'll create

| Resource | Purpose | Approx. cost |
|---|---|---|
| Azure Container Registry (Basic) | Stores Docker images | ~$5/mo |
| Container Apps Environment | Shared host for the two services | ~$0–3/mo |
| Container App: `face-recognition` | FastAPI proxy to CompreFace | ~$0–2/mo (scale-to-zero) |
| Container App: `stream-processor` | RTSP + MJPEG live feed | ~$10–13/mo (always-on) |

---

## 0. One-time: register resource providers

Portal path: **Subscription → Resource providers**

1. Go to https://portal.azure.com → search **Subscriptions** → click your subscription.
2. Left blade → **Resource providers**.
3. In the filter box, search and **Register** each of these (status must read **Registered**):
   - `Microsoft.ContainerRegistry`
   - `Microsoft.App`
   - `Microsoft.OperationalInsights`
   - `Microsoft.Web` (usually already registered)

Registration takes 1–3 minutes. Refresh until each shows **Registered**.

> If the **Register** button is greyed out, your account is not Contributor/Owner on the subscription. Ask an admin to do this once.

---

## 1. Create the Resource Group

1. Portal → **Resource groups** → **+ Create**.
2. **Subscription**: yours · **Name**: `rg-personid` · **Region**: `Central India` (same region as your API/SWA).
3. **Review + create** → **Create**.

---

## 2. Create the Container Registry (ACR)

1. Portal → **Create a resource** → search **Container Registry** → **Create**.
2. **Basics tab**
   - Resource group: `rg-personid`
   - Registry name: e.g. `personidacr<random>` (must be globally unique, lowercase letters/numbers only)
   - Location: `Central India`
   - Pricing plan: **Basic**
3. Click **Review + create** → **Create**. Wait ~30 s.
4. Once deployed, open the registry → **Settings → Access keys** → toggle **Admin user = Enabled**.
5. Copy and keep handy:
   - **Login server** (e.g. `personidacr1234.azurecr.io`)
   - **Username**
   - **password** (or password2)

---

## 3. Build the two images and push to ACR

You have three ways. Pick whichever you can use:

### Option A — Cloud Shell (recommended, no local Docker)

1. Open https://shell.azure.com (top-right toolbar icon in the portal). Choose **Bash**.
2. Upload your source: click the **Upload/Download files** icon → upload a zip of `src/PersonIdentificationSystem.Python/` (or `git clone` your repo there).
3. Run:
   ```bash
   ACR=personidacr1234            # your registry name (without .azurecr.io)
   cd PersonIdentificationSystem.Python/face_recognition_service
   az acr build -t face-recognition:v1 -r $ACR .

   cd ../stream_processor
   az acr build -t stream-processor:v1 -r $ACR .
   ```
   This builds inside Azure — no Docker needed.

### Option B — GitHub Actions
- Push code to GitHub → in the ACR blade, **Tasks → + Add → GitHub workflow** → wizard creates a build pipeline on every commit.

### Option C — Local Docker
```powershell
docker login personidacr1234.azurecr.io      # use the admin creds from step 2
docker build -t personidacr1234.azurecr.io/face-recognition:v1 .\PersonIdentificationSystem.Python\face_recognition_service
docker push personidacr1234.azurecr.io/face-recognition:v1
docker build -t personidacr1234.azurecr.io/stream-processor:v1 .\PersonIdentificationSystem.Python\stream_processor
docker push personidacr1234.azurecr.io/stream-processor:v1
```

After this, in the portal go to **ACR → Repositories** and confirm you see:
- `face-recognition` → tag `v1`
- `stream-processor` → tag `v1`

---

## 4. Create the Container Apps Environment

1. Portal → **Create a resource** → search **Container Apps Environment** → **Create**.
2. **Basics**
   - Resource group: `rg-personid`
   - Environment name: `cae-personid`
   - Region: `Central India`
3. **Monitoring tab**
   - **Logs destination**: **None** (saves ~$3/mo for a POC). Switch to Log Analytics later if you want logs.
4. **Networking**: leave defaults (public, no VNet).
5. **Review + create** → **Create**.

---

## 5. Deploy `face-recognition` Container App (scale-to-zero)

Portal → **Create a resource** → **Container App** → **Create**.

### Basics
| Field | Value |
|---|---|
| Resource group | `rg-personid` |
| Container app name | `face-recognition` |
| Region | `Central India` |
| Container Apps Environment | `cae-personid` |

### Container tab
| Field | Value |
|---|---|
| Use quickstart image | **Off** |
| Image source | **Azure Container Registry** |
| Registry | `personidacr1234.azurecr.io` |
| Image | `face-recognition` |
| Image tag | `v1` |
| CPU and memory | **0.25 CPU / 0.5 Gi** |

**Environment variables** (click **+ Add** for each):

| Name | Value |
|---|---|
| `COMPREFACE_URL` | `http://98.83.137.214:8000/` |
| `COMPREFACE_API_KEY` | `df7026b0-adf6-4e6d-9334-60b0a1226bfc` |
| `CONFIDENCE_THRESHOLD` | `0.90` |
| `MIN_FACE_SIZE_PX` | `60` |
| `DEDUP_COOLDOWN_SECONDS` | `30` |
| `DOTNET_API_URL` | `https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net/api` |

### Ingress tab
| Field | Value |
|---|---|
| Ingress | **Enabled** |
| Ingress traffic | **Accepting traffic from anywhere** |
| Ingress type | **HTTP** |
| Transport | **Auto** |
| Target port | **8000** |

### Scale tab
| Field | Value |
|---|---|
| Min replicas | **0** |
| Max replicas | **3** |

Click **Review + create** → **Create**. Wait ~1 min.

After deploy:
- Open the Container App → **Overview** → copy the **Application Url** (looks like `https://face-recognition.<random>.centralindia.azurecontainerapps.io`). **Save it as `FACE_URL`.**
- Test: open `FACE_URL/health` in a browser → expect `{"status":"healthy","compreface_reachable":true}`.

---

## 6. Deploy `stream-processor` Container App (always-on)

Repeat **step 5** with these changes:

### Basics
- Container app name: `stream-processor`

### Container tab
- Image: `stream-processor`, tag: `v1`
- CPU / memory: **0.5 CPU / 1.0 Gi**
- Environment variables:

| Name | Value |
|---|---|
| `API_BASE_URL` | `https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net/api` |
| `FACE_SERVICE_URL` | *the* `FACE_URL` *from step 5* |
| `TARGET_FPS` | `4` |
| `LOG_LEVEL` | `INFO` |

### Ingress tab
- Target port: **8085** (verify this matches the port in `mjpeg_server.py`)
- Ingress: **External**

### Scale tab
- Min replicas: **1**
- Max replicas: **1**

Create. Copy the **Application Url** → **save it as `STREAM_URL`**.

> Important: ACA can only expose **one** external port per app. If your MJPEG server runs on a different port, change Target port to match.

> The RTSP cameras must be reachable from Azure. If they're on a private LAN, this step won't work without a VPN/public NVR.

---

## 7. Wire the .NET API to the new face service

Portal → **App Services → `citycopapi`**.

### 7a. App settings

1. Left blade → **Settings → Environment variables** (or **Configuration** in older UI).
2. Click **+ Add** and create (or update) these settings:

| Name | Value |
|---|---|
| `PythonService__BaseUrl` | `https://<FACE_URL>` *(no trailing slash, no `/api`)* |
| `Cors__AllowedOrigins__0` | `http://localhost:4200` |
| `Cors__AllowedOrigins__1` | `https://red-mud-05244a700.7.azurestaticapps.net` |

3. Click **Apply** → **Confirm**.

### 7b. Enable Web Sockets (needed for SignalR)

1. Left blade → **Settings → Configuration → General settings**.
2. **Web sockets** → **On** → **Save**.

### 7c. Restart

1. **Overview** → **Restart**.

---

## 8. Update the Angular UI and redeploy to Static Web Apps

1. Open `src/PersonIdentificationSystem.UI/src/environments/environment.prod.ts` locally and set:
   ```ts
   export const environment = {
     production: true,
     apiBaseUrl:    'https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net/api',
     signalrHubUrl: 'https://citycopapi-brf5dbfwdzehg3b7.centralindia-01.azurewebsites.net/hubs/detections',
     mjpegBaseUrl:  'https://<STREAM_URL>',   // from step 6
   };
   ```
2. Commit & push to the branch that the Static Web App's GitHub Action watches. The Action will build with `--configuration production` and publish to the SWA automatically.
3. Verify: portal → **Static Web Apps → red-mud-05244a700 → Deployment history**, wait until the latest run shows **Succeeded**.

---

## 9. Enroll faces in CompreFace (one-time)

1. Open the UI: https://red-mud-05244a700.7.azurestaticapps.net
2. Or trigger sync directly from your browser:
   ```
   POST https://<FACE_URL>/api/sync-embeddings
   ```
   (Use Postman / curl / browser dev tools.)

---

## 10. Smoke tests

| Test | Expected |
|---|---|
| Open `https://<FACE_URL>/health` | `compreface_reachable: true` |
| Open the SWA URL | Loads dashboard without CORS errors in DevTools console |
| Navigate to Live Monitoring | MJPEG feed renders (if cameras reachable) |
| Stream processor logs (portal → Container App → **Logs → Console**) | Sees `Detected N face(s)` messages |

---

## 11. Updating to a new image version

1. Build & push a new tag (e.g. `v2`) using the same method from step 3.
2. Portal → **Container App → Revisions → + Create new revision**.
3. **Container image** → change tag from `v1` to `v2`.
4. **Create** → wait ~30 s → traffic shifts to the new revision.

You can also enable **Continuous deployment** under **Settings → Continuous deployment** so each push to ACR auto-deploys.

---

## 12. Save money / stop the bill

- **Stop the stream processor** when not demoing:
  - Container App → **Scale** → **Min replicas = 0**, **Max replicas = 0** → **Save**. Saves ~$10/mo.
- **Set logs to None** on the Container Apps environment.
- **Delete the resource group** when finished: **Resource groups → rg-personid → Delete resource group**.

---

## Troubleshooting (portal-only)

| Symptom | Where to look |
|---|---|
| Container App stuck at **Activating** / **Failed** | Container App → **Revisions** → click failing revision → **Console logs** |
| Crashing on startup | Container App → **Monitoring → Log stream** |
| 5xx from face service | Hit `/health` → check `compreface_reachable`. If `false`, the CompreFace VM at `20.219.170.37` is down or not reachable from Azure |
| UI CORS error in browser | `citycopapi → Environment variables`: confirm `Cors__AllowedOrigins__1` is exactly the SWA URL (no trailing slash). Restart App Service after change |
| SignalR not connecting | Confirm **Web sockets = On** under App Service → Configuration → General settings |
| MJPEG blocked in browser | Browser blocks mixed content. `mjpegBaseUrl` must be **https://**. ACA gives HTTPS automatically — just use the **Application Url** from step 6 |
| Image pull fails | Container App → **Settings → Containers → Edit and deploy**: confirm registry server, username, password are correct; or set **System-assigned identity** + grant `AcrPull` on the registry |
