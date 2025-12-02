import { TestBed } from '@angular/core/testing';

import { DrawsService } from './draws.service';

describe('DrawsService', () => {
  let service: DrawsService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(DrawsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
