import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { StreamService } from '../../core/services/stream.service';
import { RTSPStream } from '../../core/models/models';

@Component({
  selector: 'app-rtsp-config',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>RTSP Stream Configuration</h1>
        <button class="btn-primary" (click)="openCreateForm()">+ Add Stream</button>
      </div>

      <!-- Add Stream Form -->
      <div class="card" *ngIf="showForm">
        <h2>{{ editMode ? 'Edit Stream' : 'New Stream' }}</h2>
        <div class="form-row">
          <label>Camera Name *</label>
          <input [(ngModel)]="formData.cameraName" placeholder="e.g. MG Road Junction" />
        </div>
        <div class="form-row">
          <div class="field-head">
            <label>Location</label>
            <button class="btn-tertiary" type="button" (click)="requestBrowserLocation()">Use Current Location</button>
          </div>
          <input [ngModel]="formData.cameraLocation" (ngModelChange)="onLocationChange($event)" placeholder="Physical location" />
        </div>
        <div class="form-row">
          <label>Mobile Number</label>
          <input [(ngModel)]="formData.cameraMobileNumber" placeholder="e.g. +919876543210" />
        </div>
        <div class="form-grid">
          <div class="form-row">
            <label>Latitude</label>
            <input type="number" [ngModel]="formData.cameraLatitude" (ngModelChange)="onLatitudeChange($event)" step="0.000001" min="-90" max="90" placeholder="e.g. 12.971599" />
          </div>
          <div class="form-row">
            <label>Longitude</label>
            <input type="number" [ngModel]="formData.cameraLongitude" (ngModelChange)="onLongitudeChange($event)" step="0.000001" min="-180" max="180" placeholder="e.g. 77.594566" />
          </div>
        </div>
        <div class="form-row geocode-status" *ngIf="isGeocoding || isLocatingCurrent || geocodeError">
          <small *ngIf="isLocatingCurrent">Detecting your current browser location...</small>
          <small *ngIf="isGeocoding">Detecting coordinates from location...</small>
          <small class="error" *ngIf="!isGeocoding && !isLocatingCurrent && geocodeError">{{ geocodeError }}</small>
        </div>
        <div class="map-preview" *ngIf="mapPreviewUrl">
          <label>Map Preview</label>
          <iframe [src]="mapPreviewUrl" title="Camera location preview" loading="lazy" referrerpolicy="no-referrer-when-downgrade"></iframe>
        </div>
        <div class="form-row">
          <label>RTSP URL *</label>
          <input [(ngModel)]="formData.rtspUrl" placeholder="rtsp://user:pass@host:554/stream" />
        </div>
        <div class="form-row">
          <label>Frame Interval (seconds)</label>
          <input type="number" [(ngModel)]="formData.frameIntervalSeconds" min="1" max="60" />
        </div>
        <div class="form-actions">
          <button class="btn-primary" (click)="saveStream()">Save</button>
          <button class="btn-secondary" (click)="cancelForm()">Cancel</button>
        </div>
      </div>

      <!-- Stream List -->
      <div class="card">
        <table *ngIf="streams.length > 0; else noStreams">
          <thead>
            <tr>
              <th>Camera</th>
              <th>Location</th>
              <th>Mobile</th>
              <th>Coordinates</th>
              <th>Status</th>
              <th>Interval</th>
              <th>Active</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let s of streams">
              <td>{{ s.cameraName }}</td>
              <td>{{ s.cameraLocation ?? '—' }}</td>
              <td>{{ s.cameraMobileNumber ?? '—' }}</td>
              <td>{{ formatCoordinates(s.cameraLatitude, s.cameraLongitude) }}</td>
              <td><span class="status-dot" [class]="'status-' + s.status.toLowerCase()">{{ statusEmoji(s.status) }} {{ s.status }}</span></td>
              <td>{{ s.frameIntervalSeconds }}s</td>
              <td>{{ s.isActive ? '✅' : '⚪' }}</td>
              <td>
                <button class="btn-icon" (click)="testConnection(s)">Test</button>
                <button class="btn-icon" (click)="editStream(s)">Edit</button>
                <button class="btn-icon btn-danger" (click)="deleteStream(s)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>
        <ng-template #noStreams>
          <p class="empty-state">No RTSP streams configured. Add your first camera above.</p>
        </ng-template>
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { color: #1a237e; margin: 0; }
    .card { background: #fff; border-radius: 8px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); margin-bottom: 24px; }
    .form-row { margin-bottom: 16px; display: flex; flex-direction: column; gap: 4px; }
    .field-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .geocode-status { margin-top: -4px; margin-bottom: 12px; }
    .geocode-status small { color: #666; }
    .geocode-status .error { color: #c62828; }
    .map-preview { margin-bottom: 16px; }
    .map-preview iframe { width: 100%; max-width: 420px; height: 220px; border: 1px solid #ddd; border-radius: 8px; }
    label { font-weight: 500; color: #444; }
    input { padding: 8px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }
    .form-actions { display: flex; gap: 12px; }
    .btn-primary { background: #1a237e; color: #fff; border: none; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
    .btn-secondary { background: #fff; border: 1px solid #ddd; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
    .btn-tertiary { background: #fff; color: #1a237e; border: 1px solid #1a237e; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; }
    .btn-icon { background: none; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; margin-right: 4px; }
    .btn-danger { color: #c62828; border-color: #c62828; }
    table { width: 100%; border-collapse: collapse; }
    th { text-align: left; padding: 12px; border-bottom: 2px solid #e0e0e0; color: #666; }
    td { padding: 12px; border-bottom: 1px solid #f0f0f0; }
    .empty-state { color: #999; text-align: center; padding: 40px; }
    .status-online { color: #2e7d32; }
    .status-offline { color: #c62828; }
    .status-error { color: #f57c00; }
    .status-unknown { color: #9e9e9e; }
  `],
})
export class RtspConfigComponent implements OnInit {
  streams: RTSPStream[] = [];
  showForm = false;
  editMode = false;
  editId: string | null = null;
  isGeocoding = false;
  isLocatingCurrent = false;
  geocodeError = '';
  mapPreviewUrl?: SafeResourceUrl;
  formData = {
    cameraName: '',
    cameraLocation: '',
    cameraMobileNumber: '',
    cameraLatitude: null as number | null,
    cameraLongitude: null as number | null,
    rtspUrl: '',
    frameIntervalSeconds: 5,
    isActive: true,
  };
  private geocodeDebounceId: ReturnType<typeof setTimeout> | null = null;
  private geocodeRequestSequence = 0;
  private applyingGeocodeValues = false;
  private hasManualCoordinateEdit = false;

  constructor(
    private streamService: StreamService,
    private http: HttpClient,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void { this.loadStreams(); }

  loadStreams(): void {
    this.streamService.getStreams().subscribe((s) => (this.streams = s));
  }

  openCreateForm(): void {
    this.showForm = true;
    this.editMode = false;
    this.editId = null;
    this.formData = {
      cameraName: '',
      cameraLocation: '',
      cameraMobileNumber: '',
      cameraLatitude: null,
      cameraLongitude: null,
      rtspUrl: '',
      frameIntervalSeconds: 5,
      isActive: true,
    };
    this.hasManualCoordinateEdit = false;
    this.geocodeError = '';
    this.isGeocoding = false;
    this.isLocatingCurrent = false;
    this.refreshMapPreview();
    this.requestBrowserLocation();
  }

  onLocationChange(value: string): void {
    this.formData.cameraLocation = value;
    this.geocodeError = '';
    this.refreshMapPreview();

    if (this.geocodeDebounceId) {
      clearTimeout(this.geocodeDebounceId);
    }

    const query = value?.trim();
    const hasAnyCoordinate = this.formData.cameraLatitude != null || this.formData.cameraLongitude != null;
    if (!query || (this.hasManualCoordinateEdit && hasAnyCoordinate)) {
      this.isGeocoding = false;
      return;
    }

    this.geocodeDebounceId = setTimeout(() => this.lookupCoordinates(query), 700);
  }

  onLatitudeChange(value: number | string | null): void {
    const normalized = this.normalizeCoordinateValue(value);
    this.formData.cameraLatitude = normalized;
    if (!this.applyingGeocodeValues && normalized != null) {
      this.hasManualCoordinateEdit = true;
    }
    this.refreshMapPreview();
  }

  onLongitudeChange(value: number | string | null): void {
    const normalized = this.normalizeCoordinateValue(value);
    this.formData.cameraLongitude = normalized;
    if (!this.applyingGeocodeValues && normalized != null) {
      this.hasManualCoordinateEdit = true;
    }
    this.refreshMapPreview();
  }

  saveStream(): void {
    const payload: Partial<RTSPStream> = {
      ...this.formData,
      cameraLatitude: this.formData.cameraLatitude ?? undefined,
      cameraLongitude: this.formData.cameraLongitude ?? undefined,
    };

    const obs = this.editMode && this.editId
      ? this.streamService.updateStream(this.editId, payload)
      : this.streamService.createStream(payload);
    obs.subscribe(() => { this.cancelForm(); this.loadStreams(); });
  }

  editStream(s: RTSPStream): void {
    this.editId = s.id;
    this.editMode = true;
    this.formData = {
      cameraName: s.cameraName,
      cameraLocation: s.cameraLocation ?? '',
      cameraMobileNumber: s.cameraMobileNumber ?? '',
      cameraLatitude: s.cameraLatitude ?? null,
      cameraLongitude: s.cameraLongitude ?? null,
      rtspUrl: s.rtspUrl,
      frameIntervalSeconds: s.frameIntervalSeconds,
      isActive: s.isActive,
    };
    this.hasManualCoordinateEdit = false;
    this.geocodeError = '';
    this.isGeocoding = false;
    this.isLocatingCurrent = false;
    this.refreshMapPreview();
    this.showForm = true;
  }

  cancelForm(): void {
    if (this.geocodeDebounceId) {
      clearTimeout(this.geocodeDebounceId);
      this.geocodeDebounceId = null;
    }
    this.showForm = false;
    this.editMode = false;
    this.editId = null;
    this.geocodeError = '';
    this.isGeocoding = false;
    this.isLocatingCurrent = false;
    this.mapPreviewUrl = undefined;
    this.hasManualCoordinateEdit = false;
    this.formData = {
      cameraName: '',
      cameraLocation: '',
      cameraMobileNumber: '',
      cameraLatitude: null,
      cameraLongitude: null,
      rtspUrl: '',
      frameIntervalSeconds: 5,
      isActive: true,
    };
  }

  testConnection(s: RTSPStream): void {
    this.streamService.testConnection(s.id).subscribe((r) => {
      alert(r.isReachable ? `✅ Connected! Latency: ${r.latencyMs}ms` : `❌ Unreachable: ${r.errorMessage}`);
      this.loadStreams();
    });
  }

  deleteStream(s: RTSPStream): void {
    if (confirm(`Delete camera "${s.cameraName}"?`)) {
      this.streamService.deleteStream(s.id).subscribe(() => this.loadStreams());
    }
  }

  statusEmoji(status: string): string {
    return { Online: '🟢', Offline: '🔴', Error: '🟡', Unknown: '⚪' }[status] ?? '⚪';
  }

  formatCoordinates(lat?: number, lng?: number): string {
    if (lat == null || lng == null) return '—';
    return `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
  }

  private lookupCoordinates(query: string): void {
    const requestId = ++this.geocodeRequestSequence;
    this.isGeocoding = true;

    this.http.get<Array<{ lat: string; lon: string }>>('https://nominatim.openstreetmap.org/search', {
      params: {
        q: query,
        format: 'jsonv2',
        limit: '1',
      },
    }).subscribe({
      next: (results) => {
        if (requestId !== this.geocodeRequestSequence) return;

        if (results.length > 0) {
          this.applyCoordinates(results[0].lat, results[0].lon);
          this.isGeocoding = false;
          this.geocodeError = '';
          return;
        }

        this.lookupCoordinatesFallback(query, requestId);
      },
      error: () => {
        if (requestId !== this.geocodeRequestSequence) return;
        this.lookupCoordinatesFallback(query, requestId);
      },
    });
  }

  requestBrowserLocation(): void {
    this.geocodeError = '';

    if (!('geolocation' in navigator)) {
      this.geocodeError = 'Geolocation is not supported by this browser.';
      return;
    }

    this.isLocatingCurrent = true;
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const lat = Number(position.coords.latitude.toFixed(6));
        const lon = Number(position.coords.longitude.toFixed(6));

        this.applyingGeocodeValues = true;
        this.formData.cameraLatitude = lat;
        this.formData.cameraLongitude = lon;
        this.applyingGeocodeValues = false;
        this.hasManualCoordinateEdit = false;
        this.refreshMapPreview();

        this.reverseGeocodeCoordinates(lat, lon);
      },
      (error) => {
        this.isLocatingCurrent = false;
        if (error.code === error.PERMISSION_DENIED) {
          this.geocodeError = 'Please allow browser location access to auto-fill coordinates.';
          return;
        }
        this.geocodeError = 'Could not get current browser location.';
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 60000,
      }
    );
  }

  private lookupCoordinatesFallback(query: string, requestId: number): void {
    this.http.get<{ features?: Array<{ geometry?: { coordinates?: number[] } }> }>('https://photon.komoot.io/api/', {
      params: {
        q: query,
        limit: '1',
      },
    }).subscribe({
      next: (result) => {
        if (requestId !== this.geocodeRequestSequence) return;

        const coordinates = result.features?.[0]?.geometry?.coordinates;
        if (coordinates && coordinates.length >= 2) {
          const [lon, lat] = coordinates;
          this.applyCoordinates(String(lat), String(lon));
          this.geocodeError = '';
        } else {
          this.geocodeError = 'Coordinates not found for this location.';
        }

        this.isGeocoding = false;
        this.refreshMapPreview();
      },
      error: () => {
        if (requestId !== this.geocodeRequestSequence) return;
        this.isGeocoding = false;
        this.geocodeError = 'Could not detect location coordinates automatically.';
      },
    });
  }

  private applyCoordinates(lat: string, lon: string): void {
    this.applyingGeocodeValues = true;
    this.formData.cameraLatitude = Number.parseFloat(lat);
    this.formData.cameraLongitude = Number.parseFloat(lon);
    this.applyingGeocodeValues = false;
    this.refreshMapPreview();
  }

  private reverseGeocodeCoordinates(lat: number, lon: number): void {
    this.http.get<{ display_name?: string; address?: Record<string, string> }>('https://nominatim.openstreetmap.org/reverse', {
      params: {
        lat: String(lat),
        lon: String(lon),
        format: 'jsonv2',
      },
    }).subscribe({
      next: (result) => {
        this.isLocatingCurrent = false;
        const friendly = this.toFriendlyLocation(result.address, result.display_name);
        if (friendly) {
          this.formData.cameraLocation = friendly;
          this.refreshMapPreview();
        }
      },
      error: () => {
        this.isLocatingCurrent = false;
      },
    });
  }

  private toFriendlyLocation(address?: Record<string, string>, displayName?: string): string {
    if (address) {
      const city = address['city'] ?? address['town'] ?? address['village'] ?? address['county'];
      const parts = [address['road'], address['suburb'], city, address['state']]
        .filter((p): p is string => !!p && p.trim().length > 0);
      const unique: string[] = [];
      for (const part of parts) {
        if (!unique.includes(part)) {
          unique.push(part);
        }
      }
      if (unique.length > 0) {
        return unique.slice(0, 3).join(', ');
      }
    }

    if (displayName?.trim()) {
      return displayName.split(',').map((p) => p.trim()).filter(Boolean).slice(0, 3).join(', ');
    }

    return '';
  }

  private normalizeCoordinateValue(value: number | string | null): number | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const parsed = typeof value === 'number' ? value : Number.parseFloat(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private refreshMapPreview(): void {
    const { cameraLatitude: lat, cameraLongitude: lng, cameraLocation: location } = this.formData;
    let embedUrl: string | null = null;

    if (lat != null && lng != null) {
      embedUrl = `https://maps.google.com/maps?q=${lat},${lng}&z=15&output=embed`;
    } else if (location.trim()) {
      embedUrl = `https://maps.google.com/maps?q=${encodeURIComponent(location)}&z=15&output=embed`;
    }

    this.mapPreviewUrl = embedUrl
      ? this.sanitizer.bypassSecurityTrustResourceUrl(embedUrl)
      : undefined;
  }
}
