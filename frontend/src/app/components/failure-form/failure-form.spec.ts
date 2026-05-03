import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FailureForm } from './failure-form';

describe('FailureForm', () => {
  let component: FailureForm;
  let fixture: ComponentFixture<FailureForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FailureForm]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FailureForm);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
