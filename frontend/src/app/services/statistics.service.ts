import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface DashboardData {
  totalFailures: number;
  avgParticipants: number;
  topParticipants: { name: string; avgWeight: number; topCount: number }[];
  factorAverages: { name: string; avgWeight: number }[];
}

@Injectable({ providedIn: 'root' })
export class StatisticsService {
  constructor(private api: ApiService) {}

  getDashboardData(): Observable<DashboardData> {
    return this.api.http.get<DashboardData>(`${this.api.baseUrl}/Statistics/dashboard`);
  }
}