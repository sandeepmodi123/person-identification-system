import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { PersonListComponent } from './person-list.component';

@NgModule({
  declarations: [PersonListComponent],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule.forChild([{ path: '', component: PersonListComponent }]),
  ],
})
export class PersonManagementModule {}
