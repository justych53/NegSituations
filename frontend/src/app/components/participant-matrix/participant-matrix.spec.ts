import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ParticipantMatrix } from './participant-matrix';

describe('ParticipantMatrix', () => {
  let component: ParticipantMatrix;
  let fixture: ComponentFixture<ParticipantMatrix>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantMatrix]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ParticipantMatrix);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
