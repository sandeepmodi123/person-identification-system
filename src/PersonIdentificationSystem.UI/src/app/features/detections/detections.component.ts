import { Component, OnInit } from '@angular/core';
import { DetectionService } from '../../core/services/detection.service';
import { Detection } from '../../core/models/models';

@Component({
  selector: 'app-detections',
  template: `
    <div class="page">
      <h1>Detection Events</h1>

      <!-- Filters -->
      <div class="card filters">
        <input [(ngModel)]="filters.minConfidence" type="number" min="0" max="1" step="0.05" placeholder="Min confidence (0-1)" />
        <select [(ngModel)]="filters.isVerified">
          <option value="">All</option>
          <option value="false">Unverified</option>
          <option value="true">Verified</option>
        </select>
        <button class="btn-primary" (click)="loadDetections()">Apply</button>
      </div>

      <!-- Detection Table -->
      <div class="card">
        <table *ngIf="detections.length > 0; else noDetections">
          <thead>
            <tr>
              <th>Person</th>
              <th>Camera</th>
              <th>Confidence</th>
              <th>Detected At</th>
              <th>Risk</th>
              <th>Verified</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let d of detections">
              <td>{{ d.personName ?? 'Unknown' }}</td>
              <td>{{ d.cameraName }}</td>
              <td>{{ (d.confidenceScore * 100).toFixed(1) }}%</td>
              <td>{{ d.detectionTimestamp | date:'medium' }}</td>
              <td>{{ d.riskLevel ?? '—' }}</td>
              <td>{{ d.isVerified ? d.verificationStatus : '⏳ Pending' }}</td>
              <td *ngIf="!d.isVerified">
                <button class="btn-icon" (click)="verify(d, 'TruePositive')">✅ True</button>
                <button class="btn-icon btn-danger" (click)="verify(d, 'FalsePositive')">❌ False</button>
              </td>
              <td *ngIf="d.isVerified">—</td>
            </tr>
          </tbody>
        </table>
        <ng-template #noDetections>
          <p class="empty-state">No detections found.</p>
        </ng-template>

        <div class="pagination" *ngIf="totalPages > 1">
          <button (click)="prevPage()" [disabled]="page === 1">‹</button>
          <span>Page {{ page }} of {{ totalPages }}</span>
          <button (click)="nextPage()" [disabled]="page === totalPages">›</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1200px; margin: 0 auto; }
    h1 { color: #1a237e; margin-bottom: 24px; }
    .card { background: #fff; border-radius: 8px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); margin-bottom: 24px; }
    .filters { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    .filters input, .filters select { padding: 8px 12px; border: 1px solid #ddd; border-radius: 4px; }
    .btn-primary { background: #1a237e; color: #fff; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; }
    .btn-icon { background: none; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; margin-right: 4px; }
    .btn-danger { color: #c62828; border-color: #c62828; }
    table { width: 100%; border-collapse: collapse; }
    th { text-align: left; padding: 12px; border-bottom: 2px solid #e0e0e0; color: #666; }
    td { padding: 12px; border-bottom: 1px solid #f0f0f0; }
    .pagination { display: flex; align-items: center; gap: 16px; margin-top: 16px; justify-content: center; }
    .empty-state { color: #999; text-align: center; padding: 40px; }
  `],
})
export class DetectionsComponent implements OnInit {
  detections: Detection[] = [];
  page = 1;
  pageSize = 20;
  totalPages = 1;
  filters: { minConfidence?: number; isVerified?: string } = {};

  constructor(private detectionService: DetectionService) {}

  ngOnInit(): void { this.loadDetections(); }

  loadDetections(): void {
    const isVerified = this.filters.isVerified ? this.filters.isVerified === 'true' : undefined;
    this.detectionService.getDetections({
      page: this.page,
      pageSize: this.pageSize,
      minConfidence: this.filters.minConfidence,
      isVerified,
    }).subscribe((r) => {
      this.detections = r.items;
      this.totalPages = r.totalPages;
    });
  }

  verify(d: Detection, status: 'TruePositive' | 'FalsePositive'): void {
    this.detectionService.verifyDetection(d.id, status).subscribe(() => this.loadDetections());
  }

  prevPage(): void { if (this.page > 1) { this.page--; this.loadDetections(); } }
  nextPage(): void { if (this.page < this.totalPages) { this.page++; this.loadDetections(); } }
}
