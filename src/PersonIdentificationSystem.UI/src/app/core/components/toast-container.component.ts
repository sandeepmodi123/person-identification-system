import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ToastService, ToastMessage } from '../services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      <div
        *ngFor="let t of toasts"
        class="toast"
        [ngClass]="'toast-' + t.type"
        (click)="dismiss(t.id)"
      >
        <strong>{{ t.title }}</strong>
        <p>{{ t.message }}</p>
      </div>
    </div>
  `,
  styles: [
    `
      .toast-container {
        position: fixed;
        top: 64px;
        right: 16px;
        z-index: 9999;
        display: flex;
        flex-direction: column;
        gap: 8px;
        max-width: 400px;
      }
      .toast {
        padding: 12px 16px;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        cursor: pointer;
        animation: slideIn 0.3s ease;
      }
      .toast-error {
        background: #ffcdd2;
        border-left: 4px solid #c62828;
        color: #b71c1c;
      }
      .toast-warning {
        background: #fff3e0;
        border-left: 4px solid #e65100;
        color: #e65100;
      }
      .toast-success {
        background: #c8e6c9;
        border-left: 4px solid #2e7d32;
        color: #1b5e20;
      }
      .toast-info {
        background: #e3f2fd;
        border-left: 4px solid #1565c0;
        color: #0d47a1;
      }
      .toast p {
        margin: 4px 0 0;
        font-size: 13px;
      }
      @keyframes slideIn {
        from {
          transform: translateX(100%);
          opacity: 0;
        }
        to {
          transform: translateX(0);
          opacity: 1;
        }
      }
    `,
  ],
})
export class ToastContainerComponent implements OnInit, OnDestroy {
  toasts: ToastMessage[] = [];
  private sub?: Subscription;

  constructor(private toastService: ToastService) {}

  ngOnInit(): void {
    this.sub = this.toastService.toast$.subscribe((toast) => {
      this.toasts.push(toast);
      setTimeout(() => this.dismiss(toast.id), toast.duration);
    });
  }

  dismiss(id: number): void {
    this.toasts = this.toasts.filter((t) => t.id !== id);
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }
}
