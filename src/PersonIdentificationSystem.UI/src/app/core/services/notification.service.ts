import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { NotificationSettings, NotificationLog, PagedResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  constructor(private api: ApiService) {}

  getSettings(): Observable<NotificationSettings> {
    return this.api.get<NotificationSettings>('/notifications/settings');
  }

  updateSettings(settings: Partial<NotificationSettings>): Observable<NotificationSettings> {
    return this.api.put<NotificationSettings>('/notifications/settings', settings);
  }

  getLogs(page = 1, pageSize = 20, status?: string): Observable<PagedResult<NotificationLog>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (status) params['status'] = status;
    return this.api.get<PagedResult<NotificationLog>>('/notifications/logs', params);
  }
}
