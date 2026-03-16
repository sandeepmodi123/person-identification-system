import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { RTSPStream } from '../models/models';

@Injectable({ providedIn: 'root' })
export class StreamService {
  constructor(private api: ApiService) {}

  getStreams(): Observable<RTSPStream[]> {
    return this.api.get<RTSPStream[]>('/rtsp-streams');
  }

  getStream(id: string): Observable<RTSPStream> {
    return this.api.get<RTSPStream>(`/rtsp-streams/${id}`);
  }

  createStream(data: Partial<RTSPStream>): Observable<RTSPStream> {
    return this.api.post<RTSPStream>('/rtsp-streams', data);
  }

  updateStream(id: string, data: Partial<RTSPStream>): Observable<RTSPStream> {
    return this.api.put<RTSPStream>(`/rtsp-streams/${id}`, data);
  }

  deleteStream(id: string): Observable<void> {
    return this.api.delete<void>(`/rtsp-streams/${id}`);
  }

  testConnection(id: string): Observable<{
    streamId: string;
    isReachable: boolean;
    latencyMs: number | null;
    testedAt: string;
    errorMessage: string | null;
  }> {
    return this.api.post(`/rtsp-streams/${id}/test-connection`, {});
  }
}
