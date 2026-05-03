import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ParticipantList } from './participant-list';

describe('ParticipantList', () => {
  let component: ParticipantList;
  let fixture: ComponentFixture<ParticipantList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ParticipantList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
