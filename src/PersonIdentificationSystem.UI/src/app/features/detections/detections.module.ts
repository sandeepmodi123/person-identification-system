import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DetectionsComponent } from './detections.component';

@NgModule({
  declarations: [DetectionsComponent],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule.forChild([{ path: '', component: DetectionsComponent }]),
  ],
})
export class DetectionsModule {}
