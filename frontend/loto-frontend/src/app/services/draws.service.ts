import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { apiUrl } from '../core/api.config';
import { Draw } from '../models/draw';
import { PagedResult } from '../models/paged-result';
import { DrawsFilter } from '../models/draws-filter';

@Injectable({
  providedIn: 'root'
})
export class DrawsService {

  constructor(private readonly http: HttpClient) { }

  getDraws(filter: DrawsFilter): Observable<PagedResult<Draw>> {
    let params = new HttpParams()
      .set('page', filter.page)
      .set('pageSize', filter.pageSize);

    if (filter.dateFrom) {
      params = params.set('dateFrom', filter.dateFrom);
    }

    if (filter.dateTo) {
      params = params.set('dateTo', filter.dateTo);
    }

    return this.http.get<PagedResult<Draw>>(apiUrl('/draws'), { params }).pipe(
      catchError(err => {
        console.error('Erreur lors de la recuperation des tirages', err);
        return throwError(() => err);
      })
    );
  }
}
