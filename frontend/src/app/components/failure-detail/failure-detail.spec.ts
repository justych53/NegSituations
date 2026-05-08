import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FailureDetailComponent } from './failure-detail';

describe('FailureDetail', () => {
  let component: FailureDetailComponent;
  let fixture: ComponentFixture<FailureDetailComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FailureDetailComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FailureDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
