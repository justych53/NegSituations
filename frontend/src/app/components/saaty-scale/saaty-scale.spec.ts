import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SaatyScale } from './saaty-scale';

describe('SaatyScale', () => {
  let component: SaatyScale;
  let fixture: ComponentFixture<SaatyScale>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SaatyScale]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SaatyScale);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
