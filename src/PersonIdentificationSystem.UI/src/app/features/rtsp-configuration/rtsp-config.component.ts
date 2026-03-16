import { Component, OnInit } from '@angular/core';
import { StreamService } from '../../core/services/stream.service';
import { RTSPStream } from '../../core/models/models';

@Component({
  selector: 'app-rtsp-config',
  template: `
    <div class="page">
      <div class="page-header">
        <h1>RTSP Stream Configuration</h1>
        <button class="btn-primary" (click)="showForm = true">+ Add Stream</button>
      </div>

      <!-- Add Stream Form -->
      <div class="card" *ngIf="showForm">
        <h2>{{ editMode ? 'Edit Stream' : 'New Stream' }}</h2>
        <div class="form-row">
          <label>Camera Name *</label>
          <input [(ngModel)]="formData.cameraName" placeholder="e.g. MG Road Junction" />
        </div>
        <div class="form-row">
          <label>Location</label>
          <input [(ngModel)]="formData.cameraLocation" placeholder="Physical location" />
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
    label { font-weight: 500; color: #444; }
    input { padding: 8px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }
    .form-actions { display: flex; gap: 12px; }
    .btn-primary { background: #1a237e; color: #fff; border: none; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
    .btn-secondary { background: #fff; border: 1px solid #ddd; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
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
  formData = { cameraName: '', cameraLocation: '', rtspUrl: '', frameIntervalSeconds: 5, isActive: true };

  constructor(private streamService: StreamService) {}

  ngOnInit(): void { this.loadStreams(); }

  loadStreams(): void {
    this.streamService.getStreams().subscribe((s) => (this.streams = s));
  }

  saveStream(): void {
    const obs = this.editMode && this.editId
      ? this.streamService.updateStream(this.editId, this.formData)
      : this.streamService.createStream(this.formData);
    obs.subscribe(() => { this.cancelForm(); this.loadStreams(); });
  }

  editStream(s: RTSPStream): void {
    this.editId = s.id;
    this.editMode = true;
    this.formData = { cameraName: s.cameraName, cameraLocation: s.cameraLocation ?? '', rtspUrl: s.rtspUrl, frameIntervalSeconds: s.frameIntervalSeconds, isActive: s.isActive };
    this.showForm = true;
  }

  cancelForm(): void {
    this.showForm = false;
    this.editMode = false;
    this.editId = null;
    this.formData = { cameraName: '', cameraLocation: '', rtspUrl: '', frameIntervalSeconds: 5, isActive: true };
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
}
