import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { RtspConfigComponent } from './rtsp-config.component';

@NgModule({
  declarations: [RtspConfigComponent],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule.forChild([{ path: '', component: RtspConfigComponent }]),
  ],
})
export class RtspConfigurationModule {}
