import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ComparisonMatrix } from './comparison-matrix';

describe('ComparisonMatrix', () => {
  let component: ComparisonMatrix;
  let fixture: ComponentFixture<ComparisonMatrix>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ComparisonMatrix]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ComparisonMatrix);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
