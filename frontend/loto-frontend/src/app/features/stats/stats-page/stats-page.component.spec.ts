import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { of } from 'rxjs';
import {
  CooccurrenceStats,
  PatternDistribution,
  StatsFrequencies,
  StatsOverview
} from '../../../models/stats.models';
import { StatsService } from '../../../services/stats.service';
import { StatsPageComponent } from './stats-page.component';

describe('StatsPageComponent', () => {
  let component: StatsPageComponent;
  let fixture: ComponentFixture<StatsPageComponent>;
  let statsServiceMock: Partial<StatsService>;

  beforeEach(async () => {
    const overviewMock: StatsOverview = {
      totalDraws: 3,
      firstDrawDate: '2024-01-01',
      lastDrawDate: '2024-01-08',
      drawsPerDayOfWeek: [{ dayName: 'Lundi', count: 2 }]
    };

    const frequenciesMock: StatsFrequencies = {
      mainNumbers: [
        { number: 1, count: 3, frequency: 0.3 },
        { number: 2, count: 2, frequency: 0.2 }
      ],
      luckyNumbers: [
        { number: 1, count: 1, frequency: 0.5 },
        { number: 2, count: 1, frequency: 0.5 }
      ]
    };

    const patternsMock: PatternDistribution = {
      sumBuckets: [{ minInclusive: 50, maxInclusive: 59, count: 1 }],
      evenCountDistribution: { 2: 1 },
      lowCountDistribution: { 3: 1 }
    };

    const cooccurrenceMock: CooccurrenceStats = {
      baseNumber: 1,
      totalDraws: 3,
      drawsContainingBase: 2,
      cooccurrences: [{ number: 2, cooccurrenceCount: 2, conditionalProbability: 0.5, globalProbability: 0.1 }]
    };

    statsServiceMock = {
      getOverview: () => of(overviewMock),
      getFrequencies: () => of(frequenciesMock),
      getPatterns: () => of(patternsMock),
      getCooccurrence: () => of(cooccurrenceMock)
    };

    await TestBed.configureTestingModule({
      declarations: [StatsPageComponent],
      imports: [CommonModule, MatCardModule, MatFormFieldModule, MatSelectModule, NoopAnimationsModule],
      providers: [{ provide: StatsService, useValue: statsServiceMock }]
    }).compileComponents();

    fixture = TestBed.createComponent(StatsPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
