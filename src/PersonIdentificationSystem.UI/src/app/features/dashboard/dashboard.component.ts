import { Component, OnInit } from '@angular/core';
import { DetectionService } from '../../core/services/detection.service';
import { StreamService } from '../../core/services/stream.service';
import { PersonService } from '../../core/services/person.service';
import { Detection, RTSPStream } from '../../core/models/models';

@Component({
  selector: 'app-dashboard',
  template: `
    <div class="dashboard">
      <h1>Dashboard</h1>

      <!-- Summary Cards -->
      <div class="summary-cards">
        <div class="card">
          <span class="card-label">Active Streams</span>
          <span class="card-value">{{ activeStreams }}</span>
        </div>
        <div class="card">
          <span class="card-label">Total Persons</span>
          <span class="card-value">{{ totalPersons }}</span>
        </div>
        <div class="card card-alert">
          <span class="card-label">Today's Detections</span>
          <span class="card-value">{{ todayDetections }}</span>
        </div>
      </div>

      <!-- Recent Detections -->
      <div class="section">
        <h2>Recent Detections</h2>
        <table *ngIf="recentDetections.length > 0; else noDetections">
          <thead>
            <tr>
              <th>Person</th>
              <th>Camera</th>
              <th>Confidence</th>
              <th>Time</th>
              <th>Risk</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let d of recentDetections" [class]="'risk-' + (d.riskLevel?.toLowerCase() ?? 'low')">
              <td>{{ d.personName ?? 'Unknown' }}</td>
              <td>{{ d.cameraName }}</td>
              <td>{{ (d.confidenceScore * 100).toFixed(1) }}%</td>
              <td>{{ d.detectionTimestamp | date:'short' }}</td>
              <td><span class="badge" [class]="'risk-badge-' + (d.riskLevel?.toLowerCase() ?? 'low')">{{ d.riskLevel }}</span></td>
            </tr>
          </tbody>
        </table>
        <ng-template #noDetections>
          <p class="empty-state">No detections yet. Configure RTSP streams and add persons to start.</p>
        </ng-template>
      </div>
    </div>
  `,
  styles: [`
    .dashboard { max-width: 1200px; margin: 0 auto; }
    h1 { color: #1a237e; margin-bottom: 24px; }
    .summary-cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 32px; }
    .card { background: #fff; border-radius: 8px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); display: flex; flex-direction: column; align-items: center; }
    .card-alert { border-left: 4px solid #f57c00; }
    .card-label { color: #666; font-size: 14px; margin-bottom: 8px; }
    .card-value { font-size: 36px; font-weight: bold; color: #1a237e; }
    .section { background: #fff; border-radius: 8px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
    h2 { color: #333; margin-bottom: 16px; }
    table { width: 100%; border-collapse: collapse; }
    th { text-align: left; padding: 12px; border-bottom: 2px solid #e0e0e0; color: #666; }
    td { padding: 12px; border-bottom: 1px solid #f0f0f0; }
    .badge { padding: 4px 8px; border-radius: 12px; font-size: 12px; font-weight: bold; }
    .risk-badge-critical { background: #ffcdd2; color: #c62828; }
    .risk-badge-high { background: #ffe0b2; color: #e65100; }
    .risk-badge-medium { background: #fff9c4; color: #f57f17; }
    .risk-badge-low { background: #c8e6c9; color: #2e7d32; }
    .empty-state { color: #999; text-align: center; padding: 40px; }
  `],
})
export class DashboardComponent implements OnInit {
  activeStreams = 0;
  totalPersons = 0;
  todayDetections = 0;
  recentDetections: Detection[] = [];

  constructor(
    private detectionService: DetectionService,
    private streamService: StreamService,
    private personService: PersonService
  ) {}

  ngOnInit(): void {
    this.loadDashboardData();
  }

  private loadDashboardData(): void {
    // Load active streams count
    this.streamService.getStreams().subscribe((streams) => {
      this.activeStreams = streams.filter((s) => s.isActive && s.status === 'Online').length;
    });

    // Load total persons
    this.personService.getPersons(1, 1, undefined, undefined, true).subscribe((result) => {
      this.totalPersons = result.totalCount;
    });

    // Load today's detections
    const today = new Date().toISOString().split('T')[0];
    this.detectionService
      .getDetections({ page: 1, pageSize: 10, fromDate: today })
      .subscribe((result) => {
        this.todayDetections = result.totalCount;
        this.recentDetections = result.items;
      });
  }
}
