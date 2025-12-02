import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PredictionsResponse } from '../models/predictions.model';

@Injectable({
  providedIn: 'root'
})
export class PredictionsService {
  constructor(private readonly http: HttpClient) { }

  generate(count: number): Observable<PredictionsResponse> {
    return this.http.post<PredictionsResponse>('/api/predictions', { count });
  }
}
