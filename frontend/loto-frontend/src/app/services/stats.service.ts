import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CooccurrenceStats,
  PatternDistribution,
  StatsFrequencies,
  StatsOverview
} from '../models/stats.models';

@Injectable({ providedIn: 'root' })
export class StatsService {
  constructor(private http: HttpClient) {}

  getOverview(): Observable<StatsOverview> {
    return this.http.get<StatsOverview>('/api/stats/overview');
  }

  getFrequencies(): Observable<StatsFrequencies> {
    return this.http.get<StatsFrequencies>('/api/stats/frequencies');
  }

  getPatterns(bucketSize?: number): Observable<PatternDistribution> {
    let params = new HttpParams();
    if (bucketSize && bucketSize > 0) {
      params = params.set('bucketSize', bucketSize);
    }
    return this.http.get<PatternDistribution>('/api/stats/patterns', { params });
  }

  getCooccurrence(baseNumber: number, top?: number): Observable<CooccurrenceStats> {
    let params = new HttpParams().set('baseNumber', baseNumber);
    if (top && top > 0) {
      params = params.set('top', top);
    }
    return this.http.get<CooccurrenceStats>('/api/stats/cooccurrence', { params });
  }
}
