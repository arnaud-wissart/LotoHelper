import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CooccurrenceStats,
  PatternDistribution,
  StatsFrequencies,
  StatsOverview
} from '../models/stats.models';
import { apiUrl } from '../core/api.config';

@Injectable({ providedIn: 'root' })
export class StatsService {
  constructor(private http: HttpClient) {}

  getOverview(): Observable<StatsOverview> {
    return this.http.get<StatsOverview>(apiUrl('/stats/overview'));
  }

  getFrequencies(): Observable<StatsFrequencies> {
    return this.http.get<StatsFrequencies>(apiUrl('/stats/frequencies'));
  }

  getPatterns(bucketSize?: number): Observable<PatternDistribution> {
    let params = new HttpParams();
    if (bucketSize && bucketSize > 0) {
      params = params.set('bucketSize', bucketSize);
    }
    return this.http.get<PatternDistribution>(apiUrl('/stats/patterns'), { params });
  }

  getCooccurrence(baseNumber: number, top?: number): Observable<CooccurrenceStats> {
    let params = new HttpParams().set('baseNumber', baseNumber);
    if (top && top > 0) {
      params = params.set('top', top);
    }
    return this.http.get<CooccurrenceStats>(apiUrl('/stats/cooccurrence'), { params });
  }
}
