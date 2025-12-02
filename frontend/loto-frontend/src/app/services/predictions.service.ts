import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PredictionRequestDto, PredictionStrategy, PredictionsResponse } from '../models/predictions.model';
import { apiUrl } from '../core/api.config';

@Injectable({
  providedIn: 'root'
})
export class PredictionsService {
  constructor(private readonly http: HttpClient) { }

  generate(request: PredictionRequestDto): Observable<PredictionsResponse> {
    return this.http.post<PredictionsResponse>(apiUrl('/predictions'), request);
  }
}
