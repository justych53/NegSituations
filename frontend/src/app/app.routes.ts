import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { FailureListComponent } from './components/failure-list/failure-list';
import { FailureFormComponent } from './components/failure-form/failure-form';
import { FactorsListComponent } from './components/factors-list/factors-list';
import { FailureDetailComponent } from './components/failure-detail/failure-detail';

export const routes: Routes = [
  { path: 'failures', component: FailureListComponent },
  { path: 'failures/new', component: FailureFormComponent },
  { path: 'failures/:id/edit', component: FailureFormComponent },
  { path: 'failures/:id', component: FailureDetailComponent },
  { path: 'factors', component: FactorsListComponent },
  { path: '', redirectTo: '/failures', pathMatch: 'full' }
];

export class AppRoutingModule { }