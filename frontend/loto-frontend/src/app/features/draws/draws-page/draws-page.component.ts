import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { Draw } from '../../../models/draw';
import { DrawsService } from '../../../services/draws.service';

@Component({
    selector: 'app-draws-page',
    templateUrl: './draws-page.component.html',
    styleUrls: ['./draws-page.component.scss'],
    standalone: false
})
export class DrawsPageComponent implements OnInit, OnDestroy {
  draws: Draw[] = [];
  isLoading = true;
  errorMessage: string | null = null;

  private sub?: Subscription;

  constructor(private readonly drawsService: DrawsService) { }

  ngOnInit(): void {
    this.sub = this.drawsService.getDraws().subscribe({
      next: draws => {
        this.draws = draws;
        this.isLoading = false;
        this.errorMessage = null;
      },
      error: err => {
        // catchError dans le service renvoie déjà [] mais on garde la gestion explicite du message.
        this.errorMessage = 'Une erreur est survenue lors du chargement des tirages.';
        console.error('Error fetching draws', err);
        this.isLoading = false;
      }
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  formatDate(value: string): string {
    return new Date(value).toLocaleDateString();
  }

  retry(): void {
    this.isLoading = true;
    this.errorMessage = null;
    this.sub?.unsubscribe();
    this.sub = this.drawsService.getDraws().subscribe({
      next: draws => {
        this.draws = draws;
        this.isLoading = false;
      },
      error: err => {
        this.errorMessage = 'Une erreur est survenue lors du chargement des tirages.';
        console.error('Error fetching draws', err);
        this.isLoading = false;
      }
    });
  }
}
