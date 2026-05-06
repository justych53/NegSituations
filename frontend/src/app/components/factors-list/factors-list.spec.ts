import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FactorsList } from './factors-list';

describe('FactorsList', () => {
  let component: FactorsList;
  let fixture: ComponentFixture<FactorsList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FactorsList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FactorsList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
