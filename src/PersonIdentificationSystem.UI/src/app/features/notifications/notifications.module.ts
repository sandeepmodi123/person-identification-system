import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NotificationsComponent } from './notifications.component';

@NgModule({
  declarations: [NotificationsComponent],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule.forChild([{ path: '', component: NotificationsComponent }]),
  ],
})
export class NotificationsModule {}
