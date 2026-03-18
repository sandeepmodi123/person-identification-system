import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../core/services/notification.service';
import { NotificationSettings, NotificationLog } from '../../core/models/models';

@Component({
  selector: 'app-notifications',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">
      <h1>Notification Settings</h1>

      <!-- Settings Form -->
      <div class="card" *ngIf="settings">
        <h2>Email Configuration</h2>
        <div class="form-row">
          <label>Recipient Emails (one per line)</label>
          <textarea [(ngModel)]="recipientsText" rows="4" placeholder="admin@example.com&#10;supervisor@example.com"></textarea>
        </div>
        <div class="form-row">
          <label>Minimum Confidence Threshold (0-1)</label>
          <input type="number" [(ngModel)]="settings.minimumConfidenceThreshold" min="0" max="1" step="0.05" />
        </div>
        <div class="form-row">
          <label>Rate Limit (minutes between notifications per person)</label>
          <input type="number" [(ngModel)]="settings.rateLimitMinutes" min="0" />
        </div>
        <div class="form-row">
          <label>
            <input type="checkbox" [(ngModel)]="settings.isEnabled" />
            Enable Notifications
          </label>
        </div>
        <div class="form-actions">
          <button class="btn-primary" (click)="saveSettings()">Save Settings</button>
          <span class="success-msg" *ngIf="saved">✅ Saved!</span>
        </div>
      </div>

      <!-- Notification Logs -->
      <div class="card">
        <h2>Delivery Logs</h2>
        <table *ngIf="logs.length > 0; else noLogs">
          <thead>
            <tr>
              <th>Recipient</th>
              <th>Sent At</th>
              <th>Status</th>
              <th>Error</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let l of logs">
              <td>{{ l.recipientEmail }}</td>
              <td>{{ l.sentTimestamp | date:'medium' }}</td>
              <td>{{ statusEmoji(l.status) }} {{ l.status }}</td>
              <td>{{ l.errorMessage ?? '—' }}</td>
            </tr>
          </tbody>
        </table>
        <ng-template #noLogs>
          <p class="empty-state">No notification logs yet.</p>
        </ng-template>
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 900px; margin: 0 auto; }
    h1 { color: #1a237e; margin-bottom: 24px; }
    .card { background: #fff; border-radius: 8px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); margin-bottom: 24px; }
    h2 { color: #333; margin-bottom: 16px; }
    .form-row { margin-bottom: 16px; display: flex; flex-direction: column; gap: 4px; }
    label { font-weight: 500; color: #444; }
    input[type=number], input[type=text], textarea { padding: 8px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }
    .form-actions { display: flex; align-items: center; gap: 16px; }
    .btn-primary { background: #1a237e; color: #fff; border: none; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
    .success-msg { color: #2e7d32; font-weight: 500; }
    table { width: 100%; border-collapse: collapse; }
    th { text-align: left; padding: 12px; border-bottom: 2px solid #e0e0e0; color: #666; }
    td { padding: 12px; border-bottom: 1px solid #f0f0f0; }
    .empty-state { color: #999; text-align: center; padding: 40px; }
  `],
})
export class NotificationsComponent implements OnInit {
  settings: NotificationSettings | null = null;
  logs: NotificationLog[] = [];
  recipientsText = '';
  saved = false;

  constructor(private notificationService: NotificationService) {}

  ngOnInit(): void {
    this.notificationService.getSettings().subscribe((s) => {
      this.settings = s;
      this.recipientsText = s.recipientEmails.join('\n');
    });
    this.notificationService.getLogs(1, 20).subscribe((r) => (this.logs = r.items));
  }

  saveSettings(): void {
    if (!this.settings) return;
    this.settings.recipientEmails = this.recipientsText.split('\n').map((e) => e.trim()).filter(Boolean);
    this.notificationService.updateSettings(this.settings).subscribe(() => {
      this.saved = true;
      setTimeout(() => (this.saved = false), 3000);
    });
  }

  statusEmoji(status: string): string {
    return { Sent: '✅', Failed: '❌', Pending: '⏳' }[status] ?? '';
  }
}
