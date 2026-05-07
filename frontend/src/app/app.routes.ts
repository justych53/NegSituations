import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { FailureListComponent } from './components/failure-list/failure-list';
import { FailureFormComponent } from './components/failure-form/failure-form';
import { ParticipantListComponent } from './components/participant-list/participant-list';
import { ParticipantFormComponent } from './components/participant-form/participant-form';
import { ComparisonMatrixComponent } from './components/comparison-matrix/comparison-matrix';
import { FactorsListComponent } from './components/factors-list/factors-list';
import { ParticipantMatrixComponent } from './components/participant-matrix/participant-matrix';

export const routes: Routes = [
  { path: 'failures', component: FailureListComponent },
  { path: 'failures/new', component: FailureFormComponent },
  { path: 'failures/:id', component: FailureFormComponent },
  { path: 'participants', component: ParticipantListComponent },
  { path: 'participants/new', component: ParticipantFormComponent },
  { path: 'participants/:id', component: ParticipantFormComponent },
  { path: 'matrix', component: ComparisonMatrixComponent },
  { path: 'factors', component: FactorsListComponent },
  { path: 'participant-matrix', component: ParticipantMatrixComponent },
  { path: '', redirectTo: '/failures', pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }