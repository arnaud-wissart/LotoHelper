import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { DrawsModule } from './features/draws/draws.module';
import { StatsModule } from './features/stats/stats.module';

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    AppRoutingModule,
    DrawsModule,
    StatsModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
