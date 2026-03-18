import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Person, PersonPhoto, PagedResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class PersonService {
  constructor(private api: ApiService) {}

  getPersons(
    page = 1,
    pageSize = 20,
    search?: string,
    riskLevel?: string,
    isActive?: boolean
  ): Observable<PagedResult<Person>> {
    const params: Record<string, string | number | boolean> = { page, pageSize };
    if (search) params['search'] = search;
    if (riskLevel) params['riskLevel'] = riskLevel;
    if (isActive !== undefined) params['isActive'] = isActive;
    return this.api.get<PagedResult<Person>>('/person', params);
  }

  getPerson(id: string): Observable<Person> {
    return this.api.get<Person>(`/person/${id}`);
  }

  createPerson(data: Partial<Person>): Observable<Person> {
    return this.api.post<Person>('/person', data);
  }

  updatePerson(id: string, data: Partial<Person>): Observable<Person> {
    return this.api.put<Person>(`/person/${id}`, data);
  }

  deletePerson(id: string): Observable<void> {
    return this.api.delete<void>(`/person/${id}`);
  }

  uploadPhoto(personId: string, file: File, isPrimary = false): Observable<PersonPhoto> {
    const form = new FormData();
    form.append('photo', file);
    form.append('isPrimary', String(isPrimary));
    return this.api.postFormData<PersonPhoto>(`/person/${personId}/photos`, form);
  }

  getPhotos(personId: string): Observable<PersonPhoto[]> {
    return this.api.get<PersonPhoto[]>(`/person/${personId}/photos`);
  }

  deletePhoto(personId: string, photoId: string): Observable<void> {
    return this.api.delete<void>(`/person/${personId}/photos/${photoId}`);
  }
}
