import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadChildren: () =>
      import('./features/dashboard/dashboard.module').then((m) => m.DashboardModule),
  },
  {
    path: 'persons',
    loadChildren: () =>
      import('./features/person-management/person-management.module').then(
        (m) => m.PersonManagementModule
      ),
  },
  {
    path: 'streams',
    loadChildren: () =>
      import('./features/rtsp-configuration/rtsp-configuration.module').then(
        (m) => m.RtspConfigurationModule
      ),
  },
  {
    path: 'detections',
    loadChildren: () =>
      import('./features/detections/detections.module').then((m) => m.DetectionsModule),
  },
  {
    path: 'notifications',
    loadChildren: () =>
      import('./features/notifications/notifications.module').then(
        (m) => m.NotificationsModule
      ),
  },
  { path: '**', redirectTo: 'dashboard' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
})
export class AppRoutingModule {}
