import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LiveMonitoringComponent } from './live-monitoring.component';

@NgModule({
  imports: [
    CommonModule,
    LiveMonitoringComponent,
    RouterModule.forChild([{ path: '', component: LiveMonitoringComponent }]),
  ],
})
export class LiveMonitoringModule {}
