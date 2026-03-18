import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { DetectionEvent } from '../models/models';

@Injectable({ providedIn: 'root' })
export class SignalrService implements OnDestroy {
  private hubConnection: signalR.HubConnection;
  private detectionSubject = new Subject<DetectionEvent>();

  detection$ = this.detectionSubject.asObservable();

  constructor() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.signalrHubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on('DetectionReceived', (event: DetectionEvent) => {
      this.detectionSubject.next(event);
    });

    this.start();
  }

  private async start(): Promise<void> {
    try {
      await this.hubConnection.start();
      console.log('SignalR connected');
    } catch (err) {
      console.error('SignalR connection failed:', err);
      setTimeout(() => this.start(), 5000);
    }
  }

  ngOnDestroy(): void {
    this.hubConnection.stop();
  }
}
