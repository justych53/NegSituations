import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private baseUrl = 'http://localhost:5279/api';

  constructor(private http: HttpClient) {}

  // FailureRecords
  getFailureRecords(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/FailureRecords`);
  }

  getFailureRecordById(id: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/FailureRecords/${id}`);
  }

  createFailureRecord(data: { descFailure: string; resInvest: string; participantIds: number[] }): Observable<any> {
    return this.http.post(`${this.baseUrl}/FailureRecords`, data);
  }

  updateFailureRecord(id: number, data: { descFailure: string; resInvest: string; participantIds: number[] }): Observable<any> {
    return this.http.put(`${this.baseUrl}/FailureRecords/${id}`, data);
  }

  deleteFailureRecord(id: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/FailureRecords/${id}`);
  }

  // Participants
  getParticipants(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/Participants`);
  }

  getParticipantById(id: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/Participants/${id}`);
  }

  createParticipant(data: { name: string; position: string }): Observable<any> {
    return this.http.post(`${this.baseUrl}/Participants`, data);
  }

  deleteParticipant(id: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/Participants/${id}`);
  }
  updateParticipant(id: number, data: any): Observable<any> {
  return this.http.put(`${this.baseUrl}/Participants/${id}`, data);
  }
  getFactors(): Observable<any[]> {
  return this.http.get<any[]>(`${this.baseUrl}/Factors`);
}

createFactor(name: string): Observable<any> {
  return this.http.post(`${this.baseUrl}/Factors`, { name });
}

deleteFactor(id: number): Observable<any> {
  return this.http.delete(`${this.baseUrl}/Factors/${id}`);
}

// Comparison Matrix
getComparisonMatrix(failureId: number): Observable<any[]> {
  return this.http.get<any[]>(`${this.baseUrl}/ComparisonMatrices/by-failure/${failureId}`);
}

saveComparisonMatrix(data: any): Observable<any> {
  return this.http.post(`${this.baseUrl}/ComparisonMatrices`, data);
}
}