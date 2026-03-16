import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  template: `
    <nav class="navbar">
      <span class="brand">🚔 Person Identification System</span>
      <ul class="nav-links">
        <li><a routerLink="/dashboard" routerLinkActive="active">Dashboard</a></li>
        <li><a routerLink="/persons" routerLinkActive="active">Persons</a></li>
        <li><a routerLink="/streams" routerLinkActive="active">Streams</a></li>
        <li><a routerLink="/detections" routerLinkActive="active">Detections</a></li>
        <li><a routerLink="/notifications" routerLinkActive="active">Notifications</a></li>
      </ul>
    </nav>
    <main class="main-content">
      <router-outlet></router-outlet>
    </main>
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
export class AppComponent {
  title = 'Person Identification System';
}
