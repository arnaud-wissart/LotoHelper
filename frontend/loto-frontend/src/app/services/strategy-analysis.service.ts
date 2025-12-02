import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { StrategyBacktestRequest, StrategyBacktestResult } from '../models/strategy-analysis.models';
import { apiUrl } from '../core/api.config';

@Injectable({
  providedIn: 'root'
})
export class StrategyAnalysisService {
  constructor(private readonly http: HttpClient) {}

  backtest(request: StrategyBacktestRequest): Observable<StrategyBacktestResult> {
    return this.http.post<StrategyBacktestResult>(apiUrl('/analysis/strategy-backtest'), request);
  }
}
