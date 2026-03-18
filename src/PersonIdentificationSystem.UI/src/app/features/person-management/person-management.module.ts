import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { PersonListComponent } from './person-list.component';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    PersonListComponent,
    RouterModule.forChild([{ path: '', component: PersonListComponent }]),
  ],
})
export class PersonManagementModule {}
