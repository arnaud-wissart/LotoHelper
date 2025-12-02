import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DrawsPageComponent } from './draws-page.component';

describe('DrawsPageComponent', () => {
  let component: DrawsPageComponent;
  let fixture: ComponentFixture<DrawsPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DrawsPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DrawsPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
