import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FailureFormComponent } from './failure-form';

describe('FailureForm', () => {
  let component: FailureFormComponent;
  let fixture: ComponentFixture<FailureFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FailureFormComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FailureFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
