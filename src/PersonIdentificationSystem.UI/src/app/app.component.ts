import { Component, OnInit, OnDestroy } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { ToastContainerComponent } from './core/components/toast-container.component';
import { SignalrService } from './core/services/signalr.service';
import { ToastService } from './core/services/toast.service';

@Component({
  selector: 'app-root',
  imports: [RouterModule, ToastContainerComponent],
  template: `
    <nav class="navbar">
      <span class="brand">Person Identification System</span>
      <ul class="nav-links">
        <li><a routerLink="/dashboard" routerLinkActive="active">Dashboard</a></li>
        <li><a routerLink="/monitoring" routerLinkActive="active">Live</a></li>
        <li><a routerLink="/persons" routerLinkActive="active">Persons</a></li>
        <li><a routerLink="/streams" routerLinkActive="active">Streams</a></li>
        <li><a routerLink="/detections" routerLinkActive="active">Detections</a></li>
        <li><a routerLink="/notifications" routerLinkActive="active">Notifications</a></li>
      </ul>
    </nav>
    <main class="main-content">
      <router-outlet></router-outlet>
    </main>
    <app-toast-container></app-toast-container>
    <footer class="footer">
      <span>Person Identification System &mdash; POC v1.0</span>
    </footer>
  `,
  styles: [`
    .navbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: #1a237e;
      color: #fff;
      padding: 0 24px;
      height: 56px;
    }
    .brand { font-weight: bold; font-size: 18px; }
    .nav-links { display: flex; gap: 16px; list-style: none; margin: 0; padding: 0; }
    .nav-links a { color: rgba(255,255,255,0.8); text-decoration: none; padding: 4px 8px; border-radius: 4px; }
    .nav-links a.active, .nav-links a:hover { color: #fff; background: rgba(255,255,255,0.15); }
    .main-content { min-height: calc(100vh - 112px); padding: 24px; background: #f5f5f5; }
    .footer { background: #1a237e; color: rgba(255,255,255,0.6); text-align: center; padding: 12px; font-size: 12px; }
  `],
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'Person Identification System';
  private sub?: Subscription;

  constructor(
    private signalr: SignalrService,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.sub = this.signalr.detection$.subscribe((event) => {
      const riskType =
        event.riskLevel === 'Critical' || event.riskLevel === 'High'
          ? 'error'
          : 'warning';
      this.toast.show(
        riskType,
        `Match: ${event.personName}`,
        `${event.cameraName} | Confidence: ${(event.confidenceScore * 100).toFixed(1)}% | Risk: ${event.riskLevel}`,
        10000
      );
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }
}
