import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { FailureListComponent } from './components/failure-list/failure-list';
import { FailureFormComponent } from './components/failure-form/failure-form';
import { FactorsListComponent } from './components/factors-list/factors-list';
import { FailureDetailComponent } from './components/failure-detail/failure-detail';
import { LoginComponent } from './components/login/login';
import { authGuard } from './services/auth.guard';
import { AdminPanelComponent } from './components/admin-panel/admin-panel';
import { DashboardComponent } from './components/dashboard/dashboard';
import { LogsListComponent } from './components/logs-list/logs-list';

export const routes: Routes = [
  { path: 'failures', component: FailureListComponent, canActivate: [authGuard] },
  { path: 'failures/new', component: FailureFormComponent, canActivate: [authGuard] },
  { path: 'failures/:id/edit', component: FailureFormComponent, canActivate: [authGuard] },
  { path: 'failures/:id', component: FailureDetailComponent, canActivate: [authGuard] },
  { path: 'factors', component: FactorsListComponent, canActivate: [authGuard] },
  { path: 'login', component: LoginComponent },
  { path: 'admin', component: AdminPanelComponent, canActivate: [authGuard] },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
  { path: 'logs', component: LogsListComponent, canActivate: [authGuard] },
  { path: '', redirectTo: '/failures', pathMatch: 'full' }
];

export class AppRoutingModule { }