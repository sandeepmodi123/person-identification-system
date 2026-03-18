import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PersonService } from '../../core/services/person.service';
import { Person } from '../../core/models/models';

@Component({
  selector: 'app-person-list',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">
      <div class="page-header">
        <h1>Person Management</h1>
        <button class="btn-primary" (click)="showAddForm = true">+ Add Person</button>
      </div>

      <!-- Add Person Form -->
      <div class="card" *ngIf="showAddForm">
        <h2>New Person</h2>
        <div class="form-row">
          <label>Name *</label>
          <input [(ngModel)]="newPerson.name" placeholder="Full name" />
        </div>
        <div class="form-row">
          <label>Description</label>
          <textarea [(ngModel)]="newPerson.description" placeholder="Case details or notes"></textarea>
        </div>
        <div class="form-row">
          <label>Risk Level</label>
          <select [(ngModel)]="newPerson.riskLevel">
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
            <option value="Critical">Critical</option>
          </select>
        </div>
        <div class="form-actions">
          <button class="btn-primary" (click)="savePerson()">Save</button>
          <button class="btn-secondary" (click)="showAddForm = false">Cancel</button>
        </div>
      </div>

      <!-- Person List -->
      <div class="card">
        <div class="search-bar">
          <input [(ngModel)]="searchTerm" (ngModelChange)="onSearch()" placeholder="Search persons..." />
        </div>

        <table *ngIf="persons.length > 0; else noPersons">
          <thead>
            <tr>
              <th>Name</th>
              <th>Risk Level</th>
              <th>Photos</th>
              <th>Status</th>
              <th>Added</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of persons">
              <td>{{ p.name }}</td>
              <td><span class="badge risk-badge-{{ p.riskLevel.toLowerCase() }}">{{ p.riskLevel }}</span></td>
              <td>{{ p.photos.length }}</td>
              <td>{{ p.isActive ? '✅ Active' : '⚪ Inactive' }}</td>
              <td>{{ p.dateAdded | date:'mediumDate' }}</td>
              <td>
                <button class="btn-icon" (click)="toggleActive(p)">{{ p.isActive ? 'Deactivate' : 'Activate' }}</button>
                <button class="btn-icon btn-danger" (click)="deletePerson(p)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>
        <ng-template #noPersons>
          <p class="empty-state">No persons found. Add your first person above.</p>
        </ng-template>

        <div class="pagination" *ngIf="totalPages > 1">
          <button (click)="prevPage()" [disabled]="page === 1">‹</button>
          <span>Page {{ page }} of {{ totalPages }}</span>
          <button (click)="nextPage()" [disabled]="page === totalPages">›</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page { max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    h1 { color: #1a237e; margin: 0; }
    .card { background: #fff; border-radius: 8px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); margin-bottom: 24px; }
    .form-row { margin-bottom: 16px; display: flex; flex-direction: column; gap: 4px; }
    label { font-weight: 500; color: #444; }
    input, textarea, select { padding: 8px 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px; }
    textarea { min-height: 80px; }
    .form-actions { display: flex; gap: 12px; }
    .btn-primary { background: #1a237e; color: #fff; border: none; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
    .btn-secondary { background: #fff; border: 1px solid #ddd; padding: 10px 20px; border-radius: 4px; cursor: pointer; }
    .btn-icon { background: none; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; margin-right: 4px; }
    .btn-danger { color: #c62828; border-color: #c62828; }
    .search-bar { margin-bottom: 16px; }
    .search-bar input { width: 100%; max-width: 400px; }
    table { width: 100%; border-collapse: collapse; }
    th { text-align: left; padding: 12px; border-bottom: 2px solid #e0e0e0; color: #666; }
    td { padding: 12px; border-bottom: 1px solid #f0f0f0; }
    .badge { padding: 4px 8px; border-radius: 12px; font-size: 12px; font-weight: bold; }
    .risk-badge-critical { background: #ffcdd2; color: #c62828; }
    .risk-badge-high { background: #ffe0b2; color: #e65100; }
    .risk-badge-medium { background: #fff9c4; color: #f57f17; }
    .risk-badge-low { background: #c8e6c9; color: #2e7d32; }
    .pagination { display: flex; align-items: center; gap: 16px; margin-top: 16px; justify-content: center; }
    .empty-state { color: #999; text-align: center; padding: 40px; }
  `],
})
export class PersonListComponent implements OnInit {
  persons: Person[] = [];
  page = 1;
  pageSize = 20;
  totalPages = 1;
  searchTerm = '';
  showAddForm = false;
  newPerson: { name: string; description: string; riskLevel: 'Low' | 'Medium' | 'High' | 'Critical' } = { name: '', description: '', riskLevel: 'Medium' };

  constructor(private personService: PersonService) {}

  ngOnInit(): void {
    this.loadPersons();
  }

  loadPersons(): void {
    this.personService.getPersons(this.page, this.pageSize, this.searchTerm || undefined).subscribe((result) => {
      this.persons = result.items;
      this.totalPages = result.totalPages;
    });
  }

  onSearch(): void {
    this.page = 1;
    this.loadPersons();
  }

  savePerson(): void {
    this.personService.createPerson(this.newPerson).subscribe(() => {
      this.showAddForm = false;
      this.newPerson = { name: '', description: '', riskLevel: 'Medium' };
      this.loadPersons();
    });
  }

  toggleActive(person: Person): void {
    this.personService.updatePerson(person.id, { isActive: !person.isActive }).subscribe(() => {
      this.loadPersons();
    });
  }

  deletePerson(person: Person): void {
    if (confirm(`Delete ${person.name}? This cannot be undone.`)) {
      this.personService.deletePerson(person.id).subscribe(() => this.loadPersons());
    }
  }

  prevPage(): void {
    if (this.page > 1) { this.page--; this.loadPersons(); }
  }

  nextPage(): void {
    if (this.page < this.totalPages) { this.page++; this.loadPersons(); }
  }
}
