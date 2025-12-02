import { Component } from '@angular/core';

@Component({
  selector: 'app-stats-page',
  templateUrl: './stats-page.component.html',
  styleUrls: ['./stats-page.component.scss'],
  standalone: false
})
export class StatsPageComponent {
  lastUpdated = new Date();
  frequencyMock = [
    { number: 1, count: 12 },
    { number: 7, count: 11 },
    { number: 18, count: 10 },
    { number: 25, count: 9 },
    { number: 42, count: 8 }
  ];

  maxCount = Math.max(...this.frequencyMock.map(x => x.count));
}
