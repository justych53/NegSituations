import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-participant-matrix',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './participant-matrix.html'
})
export class ParticipantMatrixComponent implements OnInit {
  failures: any[] = [];
  participants: any[] = [];
  selectedFailureId: number | null = null;
  filteredParticipants: any[] = [];
  scores: { [key: string]: number } = {};
  saved = false;

  normalizedMatrix: number[][] = [];
  weights: number[] = [];
  showResults = false;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getFailureRecords().subscribe(data => this.failures = data);
    this.api.getParticipants().subscribe(data => this.participants = data);
  }

  onFailureSelect(): void {
    if (!this.selectedFailureId) {
      this.filteredParticipants = [];
      this.scores = {};
      this.showResults = false;
      return;
    }

    const failureId = this.selectedFailureId;

    // Получаем участников конкретного отказа
    this.api.getFailureRecordById(failureId).subscribe(record => {
      this.filteredParticipants = record.failureParticipants?.map((fp: any) => ({
        id: fp.participantId || fp.participant?.id,
        name: fp.participant?.name || 'Неизвестно',
        position: fp.participant?.position || ''
      })) || [];

      if (this.filteredParticipants.length < 2) {
        this.scores = {};
        this.showResults = false;
        return;
      }

      // Загружаем сохранённую матрицу
      this.api.getParticipantMatrix(failureId).subscribe(matrix => {
        this.scores = {};
        for (let i = 0; i < this.filteredParticipants.length; i++) {
          for (let j = i + 1; j < this.filteredParticipants.length; j++) {
            const key = `${this.filteredParticipants[i].id}_${this.filteredParticipants[j].id}`;
            this.scores[key] = 1;
          }
        }

        for (const entry of matrix) {
          const key = `${entry.participantAId}_${entry.participantBId}`;
          this.scores[key] = entry.score;
        }

        this.calculate();
      });
    });
  }

  getScore(aId: number, bId: number): number {
    return this.scores[`${aId}_${bId}`] || 1;
  }

  setScore(aId: number, bId: number, value: number): void {
    this.scores[`${aId}_${bId}`] = value;
    this.calculate();
  }

  getInverseScore(aId: number, bId: number): number {
    return 1 / this.getScore(aId, bId);
  }

  buildFullMatrix(): number[][] {
    const n = this.filteredParticipants.length;
    const matrix: number[][] = Array.from({ length: n }, () => Array(n).fill(1));

    for (let i = 0; i < n; i++) {
      for (let j = i + 1; j < n; j++) {
        const score = this.getScore(this.filteredParticipants[i].id, this.filteredParticipants[j].id);
        matrix[i][j] = score;
        matrix[j][i] = 1 / score;
      }
    }
    return matrix;
  }

  calculate(): void {
    if (this.filteredParticipants.length < 2) {
      this.showResults = false;
      return;
    }

    const matrix = this.buildFullMatrix();
    const n = this.filteredParticipants.length;

    const colSums: number[] = Array(n).fill(0);
    for (let j = 0; j < n; j++) {
      for (let i = 0; i < n; i++) {
        colSums[j] += matrix[i][j];
      }
    }

    this.normalizedMatrix = Array.from({ length: n }, () => Array(n).fill(0));
    for (let i = 0; i < n; i++) {
      for (let j = 0; j < n; j++) {
        this.normalizedMatrix[i][j] = matrix[i][j] / colSums[j];
      }
    }

    this.weights = Array(n).fill(0);
    for (let i = 0; i < n; i++) {
      let sum = 0;
      for (let j = 0; j < n; j++) {
        sum += this.normalizedMatrix[i][j];
      }
      this.weights[i] = sum / n;
    }

    this.showResults = true;
  }

  getWeightPercent(index: number): string {
    return (this.weights[index] * 100).toFixed(2);
  }

  getSortedWeights(): { name: string; weight: number }[] {
    return this.filteredParticipants
      .map((p, i) => ({ name: `${p.name} (${p.position})`, weight: this.weights[i] }))
      .sort((a, b) => b.weight - a.weight);
  }

  save(): void {
    if (!this.selectedFailureId) {
      alert('Сначала выберите отказ');
      return;
    }

    const entries = Object.entries(this.scores).map(([key, score]) => {
      const [a, b] = key.split('_').map(Number);
      return { participantAId: a, participantBId: b, score };
    });

    this.api.saveParticipantMatrix({
      failureRecordId: this.selectedFailureId,
      entries
    }).subscribe(() => {
      this.saved = true;
      setTimeout(() => this.saved = false, 3000);
    });
  }
}