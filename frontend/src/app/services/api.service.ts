import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private baseUrl = 'http://89.108.78.139:5279/api';

  constructor(private http: HttpClient) {}

  // FailureRecords
  getFailureRecords(): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/FailureRecords`);
  }

  getFailureRecordById(id: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/FailureRecords/${id}`);
  }

  createFailureRecord(data: { 
  descFailure: string; 
  resInvest: string; 
  participants: { name: string; position: string }[] 
}): Observable<any> {
  return this.http.post(`${this.baseUrl}/FailureRecords`, data);
}

  updateFailureRecord(id: number, data: { 
  descFailure: string; 
  resInvest: string; 
  participants: { name: string; position: string }[] 
}): Observable<any> {
  return this.http.put(`${this.baseUrl}/FailureRecords/${id}`, data);
}

  deleteFailureRecord(id: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/FailureRecords/${id}`);
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
getParticipantMatrix(failureId: number): Observable<any[]> {
  return this.http.get<any[]>(`${this.baseUrl}/ParticipantMatrices/by-failure/${failureId}`);
}

saveParticipantMatrix(data: any): Observable<any> {
  return this.http.post(`${this.baseUrl}/ParticipantMatrices`, data);
}

getParticipantMatrixByFactor(failureId: number, factorId: number): Observable<any[]> {
  return this.http.get<any[]>(`${this.baseUrl}/ParticipantMatrices/by-failure/${failureId}/factor/${factorId}`);
}

saveParticipantMatrixByFactor(data: { failureRecordId: number; factorId: number; entries: any[] }): Observable<any> {
  return this.http.post(`${this.baseUrl}/ParticipantMatrices/by-factor`, data);
}
detectParticipants(description: string, result: string): Observable<{ name: string; position: string }[]> {
  return this.http.post<{ name: string; position: string }[]>(
    `${this.baseUrl}/FailureRecords/detect-participants`,
    { description, result }
  );
}
autoFillMatrix(failureId: number): Observable<any> {
  return this.http.post(`${this.baseUrl}/FailureRecords/${failureId}/auto-fill-matrix`, {});
}
getQuestionnaireAnswers(failureId: number): Observable<{ id: number; participantId: number; answer: string }[]> {
  return this.http.get<{ id: number; participantId: number; answer: string }[]>(
    `${this.baseUrl}/Questionnaire/by-failure/${failureId}`
  );
}

saveQuestionnaireAnswers(data: { failureRecordId: number; answers: { participantId: number; answer: string }[] }): Observable<any> {
  return this.http.post(`${this.baseUrl}/Questionnaire/save`, data);
}

getAnalysisRaw(description: string): Observable<any> {
  return this.http.post(`${this.baseUrl}/FailureRecords/analyze-raw`, { description });
}
}