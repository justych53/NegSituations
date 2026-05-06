import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-comparison-matrix',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './comparison-matrix.html'
})
export class ComparisonMatrixComponent implements OnInit {
  failures: any[] = [];
  factors: any[] = [];
  selectedFailureId: number | null = null;
  scores: { [key: string]: number } = {};
  saved = false;

  // Результаты расчётов
  normalizedMatrix: number[][] = [];
  weights: number[] = [];
  showResults = false;

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.api.getFailureRecords().subscribe(data => this.failures = data);
    this.api.getFactors().subscribe(data => this.factors = data);
  }

  onFailureSelect(): void {
    if (!this.selectedFailureId) {
      this.scores = {};
      this.showResults = false;
      return;
    }

    this.api.getComparisonMatrix(this.selectedFailureId).subscribe(matrix => {
      this.scores = {};
      for (let i = 0; i < this.factors.length; i++) {
        for (let j = i + 1; j < this.factors.length; j++) {
          const key = `${this.factors[i].id}_${this.factors[j].id}`;
          this.scores[key] = 1;
        }
      }

      for (const entry of matrix) {
        const key = `${entry.factorAId}_${entry.factorBId}`;
        this.scores[key] = entry.score;
      }

      this.calculate();
    });
  }

  getScore(factorAId: number, factorBId: number): number {
    return this.scores[`${factorAId}_${factorBId}`] || 1;
  }

  setScore(factorAId: number, factorBId: number, value: number): void {
    this.scores[`${factorAId}_${factorBId}`] = value;
    this.calculate();
  }

  // Выбор из шкалы Саати
  selectScore(factorAId: number, factorBId: number, value: number): void {
    this.setScore(factorAId, factorBId, value);
  }

  // Получить обратное значение для нижнего треугольника
  getInverseScore(factorAId: number, factorBId: number): number {
    const score = this.getScore(factorAId, factorBId);
    return 1 / score;
  }

  // Построить полную матрицу
  buildFullMatrix(): number[][] {
    const n = this.factors.length;
    const matrix: number[][] = Array.from({ length: n }, () => Array(n).fill(1));

    for (let i = 0; i < n; i++) {
      for (let j = i + 1; j < n; j++) {
        const score = this.getScore(this.factors[i].id, this.factors[j].id);
        matrix[i][j] = score;
        matrix[j][i] = 1 / score;
      }
    }
    return matrix;
  }

  // Нормирование и расчёт весов
  calculate(): void {
    const matrix = this.buildFullMatrix();
    const n = this.factors.length;

    // Суммы по столбцам
    const colSums: number[] = Array(n).fill(0);
    for (let j = 0; j < n; j++) {
      for (let i = 0; i < n; i++) {
        colSums[j] += matrix[i][j];
      }
    }

    // Нормированная матрица
    this.normalizedMatrix = Array.from({ length: n }, () => Array(n).fill(0));
    for (let i = 0; i < n; i++) {
      for (let j = 0; j < n; j++) {
        this.normalizedMatrix[i][j] = matrix[i][j] / colSums[j];
      }
    }

    // Веса (среднее по строкам нормированной матрицы)
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

  // Получить процент из веса
  getWeightPercent(index: number): string {
    return (this.weights[index] * 100).toFixed(2);
  }
  
  getSortedWeights(): { name: string; weight: number }[] {
  const result = this.factors.map((f, i) => ({
    name: f.name,
    weight: this.weights[i]
  }));
  return result.sort((a, b) => b.weight - a.weight);
}

  save(): void {
    if (!this.selectedFailureId) {
      alert('Сначала выберите отказ');
      return;
    }

    const entries = Object.entries(this.scores).map(([key, score]) => {
      const [a, b] = key.split('_').map(Number);
      return { factorAId: a, factorBId: b, score };
    });

    this.api.saveComparisonMatrix({
      failureRecordId: this.selectedFailureId,
      entries
    }).subscribe(() => {
      this.saved = true;
      setTimeout(() => this.saved = false, 3000);
    });
  }
}