import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-failure-form',
  standalone: true,
  imports: [FormsModule, CommonModule, RouterLink],
  templateUrl: './failure-form.html'
})
export class FailureFormComponent implements OnInit {
  isEdit = false;
  recordId?: number;
  descFailure = '';
  resInvest = '';
  participants: { name: string; position: string }[] = [];
  allFactors: any[] = [];
  selectedFactorIds: number[] = [];
  saving = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.api.getFactors().subscribe(data => this.allFactors = data);

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam && idParam !== 'new') {
      this.isEdit = true;
      this.recordId = +idParam;
      
      this.api.getFailureRecordById(this.recordId).subscribe(record => {
        this.descFailure = record.descFailure;
        this.resInvest = record.resInvest;
        this.participants = record.participants?.map((p: any) => ({
          name: p.name,
          position: p.position
        })) || [];
        this.selectedFactorIds = record.factors?.map((f: any) => f.id) || [];
      });
    }
  }

  addParticipant(): void {
    this.participants.push({ name: '', position: '' });
  }

  removeParticipant(index: number): void {
    this.participants.splice(index, 1);
  }

  toggleFactor(id: number): void {
    const idx = this.selectedFactorIds.indexOf(id);
    if (idx > -1) {
      this.selectedFactorIds.splice(idx, 1);
    } else {
      this.selectedFactorIds.push(id);
    }
  }

  isFactorSelected(id: number): boolean {
    return this.selectedFactorIds.includes(id);
  }

  save(): void {
    if (!this.descFailure.trim() || !this.resInvest.trim()) {
      alert('Заполните описание и результат');
      return;
    }

    if (this.participants.length === 0) {
      alert('Добавьте хотя бы одного участника');
      return;
    }

    for (const p of this.participants) {
      if (!p.name.trim() || !p.position.trim()) {
        alert('Заполните имя и должность всех участников');
        return;
      }
    }

    if (this.saving) return;
    this.saving = true;

    const data = {
      descFailure: this.descFailure,
      resInvest: this.resInvest,
      participants: this.participants,
      factorIds: this.selectedFactorIds
    };

    if (this.isEdit && this.recordId) {
      this.api.updateFailureRecord(this.recordId, data).subscribe({
        next: () => {
          this.saving = false;
          alert('Запись об отказе обновлена');
          this.router.navigate(['/failures']);
        },
        error: (err) => {
          this.saving = false;
          alert('Ошибка: ' + err.message);
        }
      });
    } else {
      this.api.createFailureRecord(data).subscribe({
        next: () => {
          this.saving = false;
          alert('Запись об отказе сохранена');
          this.router.navigate(['/failures']);
        },
        error: (err) => {
          this.saving = false;
          alert('Ошибка: ' + err.message);
        }
      });
    }
  }
}