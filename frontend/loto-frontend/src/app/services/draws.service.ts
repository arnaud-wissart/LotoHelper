import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of } from 'rxjs';
import { Draw } from '../models/draw';

@Injectable({
  providedIn: 'root'
})
export class DrawsService {

  constructor(private readonly http: HttpClient) { }

  getDraws(): Observable<Draw[]> {
    return this.http.get<Draw[]>('/api/draws').pipe(
      catchError(err => {
        console.error('Error fetching draws', err);
        return of([]);
      })
    );
  }
}
