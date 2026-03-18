import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RTSPStream, DetectionEvent } from '../../core/models/models';
import { StreamService } from '../../core/services/stream.service';
import { SignalrService } from '../../core/services/signalr.service';

@Component({
  selector: 'app-live-monitoring',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    <div class="monitoring-container">
      <div class="page-header">
        <h1>Live Monitoring</h1>
        <span class="live-badge">LIVE</span>
      </div>

      <div class="stream-grid" *ngIf="activeStreams.length > 0; else noStreams">
        <div class="stream-card" *ngFor="let s of activeStreams">
          <div class="stream-header">
            <span class="camera-name">{{ s.cameraName }}</span>
            <span class="camera-location">{{ s.cameraLocation || 'Unknown' }}</span>
          </div>
          <img
            [src]="getMjpegUrl(s.id)"
            [alt]="s.cameraName"
            class="stream-feed"
            (error)="onStreamError($event)"
          />
        </div>
      </div>

      <ng-template #noStreams>
        <div class="empty-state">
          <p>No active streams found. Add and activate streams in the Streams configuration.</p>
        </div>
      </ng-template>

      <div class="detection-panel">
        <h2>Live Detection Feed</h2>
        <div *ngIf="liveDetections.length === 0" class="empty-detections">
          Waiting for detections...
        </div>
        <div
          *ngFor="let d of liveDetections"
          class="detection-entry"
          [ngClass]="'risk-' + d.riskLevel.toLowerCase()"
        >
          <div class="detection-info">
            <strong>{{ d.personName }}</strong>
            <span class="detection-camera">{{ d.cameraName }}</span>
          </div>
          <div class="detection-meta">
            <span class="confidence">{{ (d.confidenceScore * 100).toFixed(1) }}%</span>
            <span class="detection-time">{{ d.detectionTimestamp | date : 'mediumTime' }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
    .monitoring-container { max-width: 1400px; margin: 0 auto; }
    .page-header { display: flex; align-items: center; gap: 12px; margin-bottom: 24px; }
    .page-header h1 { margin: 0; color: #1a237e; }
    .live-badge {
      background: #c62828; color: white; padding: 4px 12px; border-radius: 12px;
      font-size: 12px; font-weight: bold; animation: pulse 2s infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.5; }
    }

    .stream-grid {
      display: grid; grid-template-columns: repeat(auto-fill, minmax(480px, 1fr));
      gap: 16px; margin-bottom: 24px;
    }
    .stream-card {
      background: white; border-radius: 12px; overflow: hidden;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    }
    .stream-header {
      padding: 12px 16px; background: #1a237e; color: white;
      display: flex; justify-content: space-between; align-items: center;
    }
    .camera-name { font-weight: 600; }
    .camera-location { font-size: 13px; opacity: 0.8; }
    .stream-feed { width: 100%; height: auto; display: block; min-height: 270px; background: #111; }

    .empty-state {
      background: white; padding: 48px; border-radius: 12px; text-align: center;
      color: #666; margin-bottom: 24px;
    }

    .detection-panel {
      background: white; border-radius: 12px; padding: 20px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    }
    .detection-panel h2 { margin: 0 0 16px; color: #1a237e; }
    .empty-detections { color: #999; font-style: italic; padding: 16px 0; }

    .detection-entry {
      display: flex; justify-content: space-between; align-items: center;
      padding: 10px 12px; border-radius: 8px; margin-bottom: 6px;
      border-left: 4px solid #ccc;
    }
    .risk-critical { border-left-color: #c62828; background: #ffebee; }
    .risk-high { border-left-color: #e65100; background: #fff3e0; }
    .risk-medium { border-left-color: #f9a825; background: #fffde7; }
    .risk-low { border-left-color: #2e7d32; background: #e8f5e9; }

    .detection-info { display: flex; flex-direction: column; gap: 2px; }
    .detection-camera { font-size: 12px; color: #666; }
    .detection-meta { display: flex; flex-direction: column; align-items: flex-end; gap: 2px; }
    .confidence { font-weight: 600; font-size: 14px; }
    .detection-time { font-size: 12px; color: #888; }
    `,
  ],
})
export class LiveMonitoringComponent implements OnInit, OnDestroy {
  activeStreams: RTSPStream[] = [];
  liveDetections: DetectionEvent[] = [];
  private sub?: Subscription;

  constructor(
    private streamService: StreamService,
    private signalr: SignalrService
  ) {}

  ngOnInit(): void {
    this.streamService.getStreams().subscribe((streams) => {
      this.activeStreams = streams.filter((s) => s.isActive);
    });

    this.sub = this.signalr.detection$.subscribe((event) => {
      this.liveDetections.unshift(event);
      if (this.liveDetections.length > 50) {
        this.liveDetections.pop();
      }
    });
  }

  getMjpegUrl(streamId: string): string {
    return `${environment.mjpegBaseUrl}/stream/${streamId}/mjpeg`;
  }

  onStreamError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.background = '#333';
    img.alt = 'Stream offline';
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }
}
