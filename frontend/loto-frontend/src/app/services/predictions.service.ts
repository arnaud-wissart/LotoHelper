import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PredictionRequestDto, PredictionStrategy, PredictionsResponse } from '../models/predictions.model';

@Injectable({
  providedIn: 'root'
})
export class PredictionsService {
  constructor(private readonly http: HttpClient) { }

  generate(count: number, strategy: PredictionStrategy): Observable<PredictionsResponse> {
    const body: PredictionRequestDto = { count, strategy };
    return this.http.post<PredictionsResponse>('/api/predictions', body);
  }
}
