import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FailureList } from './failure-list';

describe('FailureList', () => {
  let component: FailureList;
  let fixture: ComponentFixture<FailureList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FailureList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FailureList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
