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
  selectedParticipantIds: number[] = [];
  allParticipants: any[] = [];
  showPercents = false;
  org = 0; tech = 0; psycho = 0;
  saving = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    // Загружаем всех участников для чекбоксов
    this.api.getParticipants().subscribe(data => {
      this.allParticipants = data;
    });

    // Проверяем, редактирование или создание
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam && idParam !== 'new') {
      this.isEdit = true;
      this.recordId = +idParam;
      
      // Загружаем существующую запись для редактирования
      this.api.getFailureRecordById(this.recordId).subscribe(record => {
        this.descFailure = record.descFailure;
        this.resInvest = record.resInvest;
        // Заполняем выбранных участников
        this.selectedParticipantIds = record.failureParticipants?.map(
          (fp: any) => fp.participantId || fp.participant?.id
        ) || [];
      });
    }
  }

  toggleParticipant(id: number): void {
    const idx = this.selectedParticipantIds.indexOf(id);
    if (idx > -1) {
      this.selectedParticipantIds.splice(idx, 1);
    } else {
      this.selectedParticipantIds.push(id);
    }
  }

  isSelected(id: number): boolean {
    return this.selectedParticipantIds.includes(id);
  }

  save(): void {
    if (!this.descFailure.trim() || !this.resInvest.trim()) {
      alert('Заполните все поля');
      return;
    }

    if (this.selectedParticipantIds.length === 0) {
      alert('Выберите хотя бы одного участника отказа');
      return;
    }

    if (this.saving) return;
    this.saving = true;

    const data = {
      descFailure: this.descFailure,
      resInvest: this.resInvest,
      participantIds: this.selectedParticipantIds
    };

    if (this.isEdit && this.recordId) {
      this.api.updateFailureRecord(this.recordId, data).subscribe({
        next: () => {
          this.saving = false;
          this.router.navigate(['/failures']);
        },
        error: (err) => {
          this.saving = false;
          alert('Ошибка при сохранении: ' + err.message);
        }
      });
    } else {
      this.api.createFailureRecord(data).subscribe({
        next: (response) => {
          this.saving = false;
          this.org = response.organizationalPercent || 0;
          this.tech = response.technicalPercent || 0;
          this.psycho = response.psychophysiologicalPercent || 0;
          this.showPercents = true;
        },
        error: (err) => {
          this.saving = false;
          alert('Ошибка при сохранении: ' + err.message);
        }
      });
    }
  }
}