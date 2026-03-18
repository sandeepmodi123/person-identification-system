import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { RtspConfigComponent } from './rtsp-config.component';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    RtspConfigComponent,
    RouterModule.forChild([{ path: '', component: RtspConfigComponent }]),
  ],
})
export class RtspConfigurationModule {}
