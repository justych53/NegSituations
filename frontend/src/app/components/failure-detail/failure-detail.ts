import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { SaatyScaleComponent } from '../saaty-scale/saaty-scale';
import { AuthService } from '../../services/auth';
import jsPDF from 'jspdf';
import { Chart, registerables } from 'chart.js';


@Component({
  selector: 'app-failure-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SaatyScaleComponent],
  templateUrl: './failure-detail.html'
})
export class FailureDetailComponent implements OnInit {
  failureId!: number;
  failure: any = null;
  activeTab: 'info' | 'factors' | 'participants' | 'questionnaire' = 'info';

  // Таблица RI
  private RI_TABLE: { [n: number]: number } = {
    1: 0.00, 2: 0.00, 3: 0.58, 4: 0.90, 5: 1.12,
    6: 1.24, 7: 1.32, 8: 1.41, 9: 1.45, 10: 1.49
  };

  // ====== Факторы ======
  factors: any[] = [];
  factorScores: { [key: string]: number } = {};
  factorNormalized: number[][] = [];
  factorWeights: number[] = [];
  factorSaved = false;
  lambdaMaxFactors: number | null = null;
  ciFactors: number | null = null;
  crFactors: number | null = null;
  isConsistentFactors: boolean = true;
  autoFilling = false;
  rawAhp: any = null; 
  // ====== Участники (общий список) ======
  participants: any[] = [];

  // ====== Матрицы участников по факторам ======
  selectedFactorIndex: number = 0;
  // scores[factorIndex][`${pAId}_${pBId}`] = score
  participantScoresByFactor: { [factorIndex: number]: { [key: string]: number } } = {};
  participantNormalizedByFactor: { [factorIndex: number]: number[][] } = {};
  participantWeightsByFactor: { [factorIndex: number]: number[] } = {};
  participantSavedByFactor: { [factorIndex: number]: boolean } = {};

  lambdaMaxByFactor: { [factorIndex: number]: number } = {};
  ciByFactor: { [factorIndex: number]: number } = {};
  crByFactor: { [factorIndex: number]: number } = {};
  isConsistentByFactor: { [factorIndex: number]: boolean } = {};

  questionnaireAnswers: { [participantId: number]: string } = {};

  // ====== Синтез ======
  synthesizedWeights: { name: string; weight: number; contributions: { factorName: string; contribution: number }[] }[] = [];
  

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService,
    private auth: AuthService
  ) {}

get canEdit(): boolean {
  if (!this.failure) return false;
  if (this.auth.isAdmin()) return true;
  const userId = this.auth.getUser()?.id;
  // Приводим к числу, т.к. createdByUserId — number, а id из токена может быть строкой
  return userId != null && this.failure.createdByUserId === +userId;
}

  get canDelete(): boolean {
    return this.canEdit;
  }

  ngOnInit(): void {
    this.failureId = +this.route.snapshot.paramMap.get('id')!;
    this.loadFailure();
  }

  loadFailure(): void {
    this.api.getFailureRecordById(this.failureId).subscribe(data => {
      this.failure = data;
      this.participants = data.participants || [];
      this.factors = data.factors || [];
      this.loadQuestionnaireAnswers();
      this.initFactorScores();
      this.loadFactorMatrix();
      this.initAllParticipantScores();
      this.loadAllParticipantMatrices();
    });
  }
  

  // ====== Факторы: инициализация, загрузка, расчёт ======
  initFactorScores(): void {
    if (this.factors.length < 2) return;
    this.factorScores = {};
    for (let i = 0; i < this.factors.length; i++) {
      for (let j = i + 1; j < this.factors.length; j++) {
        this.factorScores[`${this.factors[i].id}_${this.factors[j].id}`] = 1;
      }
    }
  }

loadFactorMatrix(): void {
  if (this.factors.length < 2) return;
  this.api.getComparisonMatrix(this.failureId).subscribe(matrix => {
    console.group('🔷 Factor matrix loaded from backend');
    console.log('Raw data:', matrix);
    this.initFactorScores();
    for (const entry of matrix) {
      const key = `${entry.factorAId}_${entry.factorBId}`;
      const score = Number(entry.score);
      if (!isNaN(score)) {
        const rounded = Number(score.toFixed(4));
        this.factorScores[key] = rounded;
        console.log(`Set ${key} = ${rounded}`);
      }
    }
    console.log('Final factorScores:', { ...this.factorScores });
    console.groupEnd();
    this.calculateFactors();
  });
}

getFactorScore(aId: number, bId: number): number {
  const val = this.factorScores[`${aId}_${bId}`];
  return val !== undefined ? Number(val) : 1;
}

setFactorScore(aId: number, bId: number, value: number): void {
  this.factorScores[`${aId}_${bId}`] = Number(value);
  this.calculateFactors();
}

  getFactorInverse(aId: number, bId: number): number {
    return 1 / this.getFactorScore(aId, bId);
  }

  saveFactors(): void {
    const entries = Object.entries(this.factorScores).map(([key, score]) => {
      const [a, b] = key.split('_').map(Number);
      return { factorAId: a, factorBId: b, score };
    });
    this.api.saveComparisonMatrix({ failureRecordId: this.failureId, entries }).subscribe(() => {
      this.factorSaved = true;
      setTimeout(() => this.factorSaved = false, 3000);
    });
  }
  calculateFactors(): void {
    if (this.factors.length < 2) return;
    const n = this.factors.length;
    const matrix = this.buildFactorMatrix();
    const result = this.calculateWeightsAndConsistency(matrix, n);

    this.factorNormalized = result.normalized;
    this.factorWeights = result.weights;
    this.lambdaMaxFactors = result.lambdaMax;
    this.ciFactors = result.ci;
    this.crFactors = result.cr;
    this.isConsistentFactors = result.isConsistent;
  }

  buildFactorMatrix(): number[][] {
    const n = this.factors.length;
    const matrix: number[][] = Array.from({ length: n }, () => Array(n).fill(1));
    for (let i = 0; i < n; i++) {
      for (let j = i + 1; j < n; j++) {
        const score = this.getFactorScore(this.factors[i].id, this.factors[j].id);
        matrix[i][j] = score;
        matrix[j][i] = 1 / score;
      }
    }
    return matrix;
  }

  getFactorPercent(i: number): string {
    return (this.factorWeights[i] * 100).toFixed(2);
  }

  getSortedFactors(): any[] {
    return this.factors
      .map((f, i) => ({ name: f.name, weight: this.factorWeights[i] }))
      .sort((a, b) => b.weight - a.weight);
  }

  // ====== Участники по факторам ======
  initAllParticipantScores(): void {
    if (this.participants.length < 2) return;
    for (let fi = 0; fi < this.factors.length; fi++) {
      this.participantScoresByFactor[fi] = {};
      for (let i = 0; i < this.participants.length; i++) {
        for (let j = i + 1; j < this.participants.length; j++) {
          this.participantScoresByFactor[fi][`${this.participants[i].id}_${this.participants[j].id}`] = 1;
        }
      }
    }
  }

  loadAllParticipantMatrices(): void {
    // Загружаем все матрицы участников для этого отказа
    // Бекенд должен поддерживать сохранение/загрузку матриц по factorIndex
    // Пока просто инициализируем — бекенд надо будет доработать
    for (let fi = 0; fi < this.factors.length; fi++) {
      this.loadParticipantMatrixForFactor(fi);
    }
  }

loadParticipantMatrixForFactor(fi: number): void {
  if (this.participants.length < 2) return;
  const factorId = this.factors[fi].id;
  this.api.getParticipantMatrixByFactor(this.failureId, factorId).subscribe(matrix => {
    console.group(`🔶 Participant matrix loaded for factor "${this.factors[fi].name}" (id=${factorId})`);
    console.log('Raw data:', matrix);
    // Инициализируем дефолтные единицы
    this.participantScoresByFactor[fi] = {};
    for (let i = 0; i < this.participants.length; i++) {
      for (let j = i + 1; j < this.participants.length; j++) {
        this.participantScoresByFactor[fi][`${this.participants[i].id}_${this.participants[j].id}`] = 1;
      }
    }
    for (const entry of matrix) {
      const key = `${entry.participantAId}_${entry.participantBId}`;
      const score = Number(entry.score);
      if (!isNaN(score)) {
        const rounded = Number(score.toFixed(4));
        this.participantScoresByFactor[fi][key] = rounded;
        console.log(`Set ${key} = ${rounded}`);
      }
    }
    console.log('Final scores for factor:', { ...this.participantScoresByFactor[fi] });
    console.groupEnd();
    this.calculateParticipantsForFactor(fi);
  });
}

  selectFactor(index: number): void {
    this.selectedFactorIndex = index;
  }

  getParticipantScore(fi: number, aId: number, bId: number): number {
    return this.participantScoresByFactor[fi]?.[`${aId}_${bId}`] || 1;
  }

  setParticipantScore(fi: number, aId: number, bId: number, value: number): void {
    if (!this.participantScoresByFactor[fi]) this.participantScoresByFactor[fi] = {};
    this.participantScoresByFactor[fi][`${aId}_${bId}`] = value;
    this.calculateParticipantsForFactor(fi);
  }

  getParticipantInverse(fi: number, aId: number, bId: number): number {
    return 1 / this.getParticipantScore(fi, aId, bId);
  }

  saveParticipantsForFactor(fi: number): void {
    const entries = Object.entries(this.participantScoresByFactor[fi] || {}).map(([key, score]) => {
      const [a, b] = key.split('_').map(Number);
      return { participantAId: a, participantBId: b, score };
    });

    this.api.saveParticipantMatrixByFactor({
      failureRecordId: this.failureId,
      factorId: this.factors[fi].id,
      entries
    }).subscribe(() => {
      this.participantSavedByFactor[fi] = true;
      setTimeout(() => this.participantSavedByFactor[fi] = false, 3000);
    });
  }

  calculateParticipantsForFactor(fi: number): void {
    if (this.participants.length < 2) return;
    const n = this.participants.length;
    const matrix = this.buildParticipantMatrix(fi);
    const result = this.calculateWeightsAndConsistency(matrix, n);

    this.participantNormalizedByFactor[fi] = result.normalized;
    this.participantWeightsByFactor[fi] = result.weights;
    this.lambdaMaxByFactor[fi] = result.lambdaMax;
    this.ciByFactor[fi] = result.ci;
    this.crByFactor[fi] = result.cr;
    this.isConsistentByFactor[fi] = result.isConsistent;

    // Пересчитываем синтез при изменении любой матрицы участников
    this.calculateSynthesis();
  }

  buildParticipantMatrix(fi: number): number[][] {
    const n = this.participants.length;
    const matrix: number[][] = Array.from({ length: n }, () => Array(n).fill(1));
    for (let i = 0; i < n; i++) {
      for (let j = i + 1; j < n; j++) {
        const score = this.getParticipantScore(fi, this.participants[i].id, this.participants[j].id);
        matrix[i][j] = score;
        matrix[j][i] = 1 / score;
      }
    }
    return matrix;
  }

  getParticipantWeightByFactor(fi: number, pi: number): number {
    return this.participantWeightsByFactor[fi]?.[pi] || 0;
  }

  getParticipantPercentByFactor(fi: number, pi: number): string {
    return ((this.participantWeightsByFactor[fi]?.[pi] || 0) * 100).toFixed(2);
  }


  getSortedParticipantsByFactor(fi: number): any[] {
    if (!this.participantWeightsByFactor[fi]) return [];
    return this.participants
      .map((p, i) => ({ name: `${p.name} (${p.position})`, weight: this.participantWeightsByFactor[fi][i] }))
      .sort((a, b) => b.weight - a.weight);
  }

  // ====== Общий метод расчёта весов и согласованности ======
  private calculateWeightsAndConsistency(matrix: number[][], n: number): {
    normalized: number[][];
    weights: number[];
    lambdaMax: number;
    ci: number;
    cr: number;
    isConsistent: boolean;
  } {
    // Суммы по столбцам
    const colSums = Array(n).fill(0);
    for (let j = 0; j < n; j++) {
      for (let i = 0; i < n; i++) colSums[j] += matrix[i][j];
    }

    // Нормированная матрица
    const normalized = Array.from({ length: n }, () => Array(n).fill(0));
    for (let i = 0; i < n; i++) {
      for (let j = 0; j < n; j++) {
        normalized[i][j] = matrix[i][j] / colSums[j];
      }
    }

    // Веса
    const weights = Array(n).fill(0);
    for (let i = 0; i < n; i++) {
      let sum = 0;
      for (let j = 0; j < n; j++) sum += normalized[i][j];
      weights[i] = sum / n;
    }

    // λmax, CI, CR
    let lambdaMax = n;
    let ci = 0;
    let cr = 0;
    let isConsistent = true;

    if (n > 2) {
      const aw: number[] = Array(n).fill(0);
      for (let i = 0; i < n; i++) {
        for (let j = 0; j < n; j++) aw[i] += matrix[i][j] * weights[j];
      }
      const lambdas = aw.map((val, i) => val / weights[i]);
      lambdaMax = lambdas.reduce((a, b) => a + b, 0) / n;
      ci = (lambdaMax - n) / (n - 1);
      const ri = this.RI_TABLE[n] || 0;
      cr = ri > 0 ? ci / ri : 0;
      isConsistent = cr < 0.10;
    }

    return { normalized, weights, lambdaMax, ci, cr, isConsistent };
  }

  // ====== Синтез итоговых весов ======
  calculateSynthesis(): void {
    if (this.factorWeights.length === 0 || this.participants.length === 0) {
      this.synthesizedWeights = [];
      return;
    }

    this.synthesizedWeights = this.participants.map((p, pi) => {
      let totalWeight = 0;
      const contributions: { factorName: string; contribution: number }[] = [];

      for (let fi = 0; fi < this.factors.length; fi++) {
        const localWeight = this.participantWeightsByFactor[fi]?.[pi] || 0;
        const contribution = this.factorWeights[fi] * localWeight;
        totalWeight += contribution;
        contributions.push({
          factorName: this.factors[fi].name,
          contribution
        });
      }

      return {
        name: `${p.name} (${p.position})`,
        weight: totalWeight,
        contributions
      };
    });

    this.synthesizedWeights.sort((a, b) => b.weight - a.weight);
  }

  getSynthesisPercent(weight: number): string {
    return (weight * 100).toFixed(1);
  }

  // ====== Удаление ======
  deleteFailure(): void {
    if (confirm('Удалить этот отказ со всеми данными?')) {
      this.api.deleteFailureRecord(this.failureId).subscribe(() => {
        this.router.navigate(['/failures']);
      });
    }
  }
autoFillMatrix(): void {
  if (confirm('Заполнить матрицы на основе данных внешнего сервиса? Текущие оценки будут заменены.')) {
    this.autoFilling = true;
    this.api.autoFillMatrix(this.failureId).subscribe({
      next: () => {
        this.autoFilling = false;
        console.log(' Auto-fill completed, reloading matrices...');
        this.loadFactorMatrix();
        for (let fi = 0; fi < this.factors.length; fi++) {
          this.loadParticipantMatrixForFactor(fi);
        }
      },
      error: (err) => {
        this.autoFilling = false;
        alert('Ошибка: ' + err.message);
      }
    });
  }
}

debugMatrices(): void {
  console.group(' Debug: current matrix states');
  console.log('Factor scores:', this.factorScores);
  console.log('Factor weights:', this.factorWeights);
  console.log('Participant scores by factor:', this.participantScoresByFactor);
  console.log('Participant weights by factor:', this.participantWeightsByFactor);
  console.groupEnd();
  alert('Данные матриц выведены в консоль (F12)');
}

loadQuestionnaireAnswers(): void {
  this.api.getQuestionnaireAnswers(this.failureId).subscribe(answers => {
    this.questionnaireAnswers = {};
    answers.forEach(a => {
      this.questionnaireAnswers[a.participantId] = a.answer;
    });
  });
}

saveQuestionnaire(): void {
  const answers = this.participants.map(p => ({
    participantId: p.id,
    answer: this.questionnaireAnswers[p.id] || ''
  }));

  this.api.saveQuestionnaireAnswers({
    failureRecordId: this.failureId,
    answers
  }).subscribe({
    next: () => alert('Анкета сохранена'),
    error: (err) => alert('Ошибка: ' + err.message)
  });
}

getAnswer(participantId: number): string {
  return this.questionnaireAnswers[participantId] || '';
}

setAnswer(participantId: number, value: string): void {
  this.questionnaireAnswers[participantId] = value;
}
  // Геттеры для безопасного доступа из шаблона
get currentParticipantWeights(): number[] {
  return this.participantWeightsByFactor[this.selectedFactorIndex] || [];
}

get currentParticipantNormalized(): number[][] {
  return this.participantNormalizedByFactor[this.selectedFactorIndex] || [];
}

get currentLambdaMax(): number | null {
  return this.lambdaMaxByFactor[this.selectedFactorIndex] ?? null;
}

get currentCi(): number | null {
  return this.ciByFactor[this.selectedFactorIndex] ?? null;
}

get currentCr(): number | null {
  return this.crByFactor[this.selectedFactorIndex] ?? null;
}

get currentIsConsistent(): boolean {
  return this.isConsistentByFactor[this.selectedFactorIndex] ?? true;
}

get currentParticipantSaved(): boolean {
  return this.participantSavedByFactor[this.selectedFactorIndex] ?? false;
}
private calcColWidths(doc: jsPDF, headers: string[], body: string[][], maxWidth: number, fontSize: number): number[] {
  doc.setFont('Roboto');
  doc.setFontSize(fontSize);
  const padding = 4;
  const colCount = headers.length;
  const rawWidths: number[] = [];
  for (let c = 0; c < colCount; c++) {
    let maxW = doc.getTextWidth(headers[c]);
    for (const row of body) {
      const w = doc.getTextWidth(row[c] ?? '');
      if (w > maxW) maxW = w;
    }
    rawWidths.push(maxW + padding);
  }
  const totalRaw = rawWidths.reduce((s, w) => s + w, 0);
  const scale = maxWidth / totalRaw;
  return rawWidths.map(w => w * scale);
}

private drawTable(
  doc: jsPDF,
  startY: number,
  headers: string[],
  body: string[][],
  colWidths: number[],
  fontSize: number,
  headerFill: [number, number, number],
  rowHeight: number
): number {
  const pageHeight = doc.internal.pageSize.getHeight();
  const margin = 14;
  const padding = 2;
  const colCount = headers.length;
  let y = startY;

  doc.setFont('Roboto');
  doc.setFontSize(fontSize);

  if (y + rowHeight > pageHeight - 15) {
    doc.addPage();
    y = 20;
  }

  let x = margin;
  for (let c = 0; c < colCount; c++) {
    doc.setFillColor(headerFill[0], headerFill[1], headerFill[2]);
    doc.setDrawColor(180, 180, 180);
    doc.rect(x, y, colWidths[c], rowHeight, 'FD');
    doc.setFont('Roboto');
    doc.setFontSize(fontSize);
    doc.text(headers[c], x + padding, y + rowHeight - padding);
    x += colWidths[c];
  }
  y += rowHeight;

  for (const row of body) {
    if (y + rowHeight > pageHeight - 15) {
      doc.addPage();
      y = 20;
    }

    x = margin;
    for (let c = 0; c < colCount; c++) {
      doc.setDrawColor(200, 200, 200);
      doc.rect(x, y, colWidths[c], rowHeight, 'D');
      doc.setFont('Roboto');
      doc.setFontSize(fontSize);
      doc.text(row[c] ?? '', x + padding, y + rowHeight - padding);
      x += colWidths[c];
    }
    y += rowHeight;
  }

  return y;
}

async exportToPdf(): Promise<void> {
  if (!this.failure) return;

  const doc = new jsPDF('p', 'mm', 'a4');
  const pageWidth = doc.internal.pageSize.getWidth();
  let y = 20;

  // ---------- РУССКИЙ ШРИФТ ----------
  await doc.addFont('/assets/fonts/Roboto-Regular.ttf', 'Roboto', 'normal');
  doc.setFont('Roboto');

  // ---------- ЗАГОЛОВОК ----------
  doc.setFontSize(16);
  doc.text(`Отказ #${this.failure.id}`, pageWidth / 2, y, { align: 'center' });
  y += 8;

  doc.setFontSize(10);
  const createdBy = this.failure.createdBy || 'неизвестен';
  const createdAt = this.failure.createdAt ? new Date(this.failure.createdAt).toLocaleString() : '—';
  doc.text(`Автор: ${createdBy}`, 14, y);
  y += 5;
  doc.text(`Дата создания: ${createdAt}`, 14, y);
  y += 10;

  // ---------- ОПИСАНИЕ И РЕЗУЛЬТАТ ----------
  doc.setFontSize(12);
  doc.text('Описание отказа', 14, y);
  y += 6;
  doc.setFontSize(10);
  const splitDesc = doc.splitTextToSize(this.failure.descFailure || '—', pageWidth - 28);
  doc.text(splitDesc, 14, y);
  y += splitDesc.length * 5 + 4;

  doc.setFontSize(12);
  doc.text('Результат расследования', 14, y);
  y += 6;
  doc.setFontSize(10);
  const splitRes = doc.splitTextToSize(this.failure.resInvest || '—', pageWidth - 28);
  doc.text(splitRes, 14, y);
  y += splitRes.length * 5 + 4;

  // ---------- УЧАСТНИКИ ----------
  doc.setFontSize(12);
  doc.text('Участники отказа', 14, y);
  y += 6;
  doc.setFontSize(10);
  if (this.failure.participants?.length > 0) {
    for (const p of this.failure.participants) {
      doc.text(`• ${p.name} (${p.position})`, 18, y);
      y += 5;
    }
  } else {
    doc.text('Нет участников', 18, y);
    y += 5;
  }
  y += 4;

  // ---------- ТАБЛИЦА ВЕСОВ ФАКТОРОВ ----------
  if (this.factorWeights.length > 0) {
    doc.setFontSize(12);
    doc.text('Веса факторов', 14, y);
    y += 6;

    const factorData = this.factors.map((f, i) => [
      f.name,
      this.factorWeights[i].toFixed(3),
      `${(this.factorWeights[i] * 100).toFixed(1)}%`
    ]);

    const cols1 = this.calcColWidths(doc, ['Фактор', 'Вес', '%'], factorData, pageWidth - 28, 9);
    y = this.drawTable(doc, y, ['Фактор', 'Вес', '%'], factorData, cols1, 9, [220, 220, 220], 7) + 6;
  }

  // ---------- ТАБЛИЦА СИНТЕЗА ----------
  if (this.synthesizedWeights.length > 0) {
    doc.setFontSize(12);
    doc.text('Распределение вины участников', 14, y);
    y += 6;

    const synthData = this.synthesizedWeights.map(sw => [
      sw.name,
      ...sw.contributions.map((c: any) => c.contribution.toFixed(3)),
      `${(sw.weight * 100).toFixed(1)}%`
    ]);
    const factorNames = this.factors.map(f => f.name);

    const synthHeaders = ['Участник', ...factorNames, 'Итог'];
    const cols2 = this.calcColWidths(doc, synthHeaders, synthData, pageWidth - 28, 8);
    y = this.drawTable(doc, y, synthHeaders, synthData, cols2, 8, [255, 235, 205], 7) + 10;
  }

  // ---------- ДИАГРАММА ВЕСОВ ФАКТОРОВ ----------
  if (this.factorWeights.length > 0) {
    const canvas = document.createElement('canvas');
    canvas.width = 400;
    canvas.height = 400;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      Chart.register(...registerables);
      const chart = new Chart(ctx, {
        type: 'pie',
        data: {
          labels: this.factors.map(f => f.name),
          datasets: [{
            data: this.factorWeights.map(w => w * 100),
            backgroundColor: ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40']
          }]
        },
        options: {
          responsive: false,
          animation: false,
          plugins: { legend: { display: true, position: 'bottom', labels: { font: { size: 14 } } } }
        }
      });

      chart.update();
      await new Promise(resolve => setTimeout(resolve, 500));
      const imgData = canvas.toDataURL('image/png');

      doc.addPage();
      doc.setFontSize(14);
      doc.text('Диаграмма весов факторов', pageWidth / 2, 20, { align: 'center' });
      doc.addImage(imgData, 'PNG', (pageWidth - 140) / 2, 30, 140, 140);
      chart.destroy();
    }
  }

  // ---------- ДИАГРАММА ВИНЫ УЧАСТНИКОВ ----------
  if (this.synthesizedWeights.length > 0) {
    const canvas = document.createElement('canvas');
    canvas.width = 500;   // чуть шире для подписей
    canvas.height = 500;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      Chart.register(...registerables);
      const chart = new Chart(ctx, {
        type: 'pie',
        data: {
          labels: this.synthesizedWeights.map(sw => sw.name),
          datasets: [{
            data: this.synthesizedWeights.map(sw => sw.weight * 100),
            backgroundColor: ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40',
                              '#C9CBCF', '#FFA07A', '#BA55D3', '#32CD32']
          }]
        },
        options: {
          responsive: false,
          animation: false,
          plugins: { legend: { display: true, position: 'bottom', labels: { font: { size: 12 } } } }
        }
      });

      chart.update();
      await new Promise(resolve => setTimeout(resolve, 500));
      const imgData = canvas.toDataURL('image/png');

      doc.addPage();
      doc.setFontSize(14);
      doc.text('Распределение вины участников', pageWidth / 2, 20, { align: 'center' });
      doc.addImage(imgData, 'PNG', (pageWidth - 150) / 2, 30, 150, 150);
      chart.destroy();
    }
  }

  // ---------- ФУТЕР ----------
  const pageCount = doc.getNumberOfPages();
  for (let i = 1; i <= pageCount; i++) {
    doc.setPage(i);
    doc.setFontSize(8);
    doc.text(
      `Сгенерировано системой расследования отказов. Страница ${i} из ${pageCount}`,
      pageWidth / 2,
      doc.internal.pageSize.getHeight() - 10,
      { align: 'center' }
    );
  }

  doc.save(`Отказ_${this.failure.id}_${createdBy}_${new Date().toISOString().slice(0, 10)}.pdf`);
}
}