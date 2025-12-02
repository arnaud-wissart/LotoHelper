import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DrawsPageComponent } from './features/draws/draws-page/draws-page.component';
import { PredictionsComponent } from './features/predictions/predictions.component';
import { StatsPageComponent } from './features/stats/stats-page/stats-page.component';

const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'draws' },
  { path: 'draws', component: DrawsPageComponent },
  { path: 'predictions', component: PredictionsComponent },
  { path: 'stats', component: StatsPageComponent },
  { path: '**', redirectTo: 'draws' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
