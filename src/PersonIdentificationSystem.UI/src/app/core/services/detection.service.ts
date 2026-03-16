import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Detection, PagedResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class DetectionService {
  constructor(private api: ApiService) {}

  getDetections(filters: {
    page?: number;
    pageSize?: number;
    streamId?: string;
    personId?: string;
    fromDate?: string;
    toDate?: string;
    minConfidence?: number;
    isVerified?: boolean;
  } = {}): Observable<PagedResult<Detection>> {
    const params: Record<string, string | number | boolean> = {
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    };
    if (filters.streamId) params['streamId'] = filters.streamId;
    if (filters.personId) params['personId'] = filters.personId;
    if (filters.fromDate) params['fromDate'] = filters.fromDate;
    if (filters.toDate) params['toDate'] = filters.toDate;
    if (filters.minConfidence !== undefined) params['minConfidence'] = filters.minConfidence;
    if (filters.isVerified !== undefined) params['isVerified'] = filters.isVerified;

    return this.api.get<PagedResult<Detection>>('/detections', params);
  }

  getDetection(id: string): Observable<Detection> {
    return this.api.get<Detection>(`/detections/${id}`);
  }

  verifyDetection(id: string, status: 'TruePositive' | 'FalsePositive', notes?: string): Observable<Detection> {
    return this.api.post<Detection>(`/detections/${id}/verify`, { status, notes });
  }
}
