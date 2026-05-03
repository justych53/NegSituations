import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-participant-form',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './participant-form.html'
})
export class ParticipantFormComponent implements OnInit {
  isEdit = false;
  participantId?: number;
  name = '';
  position = '';

  constructor(
    private router: Router,
    private route: ActivatedRoute,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam && idParam !== 'new') {
      this.isEdit = true;
      this.participantId = +idParam;
      this.api.getParticipantById(this.participantId).subscribe(p => {
        this.name = p.name;
        this.position = p.position;
      });
    }
  }

  save(): void {
    if (!this.name.trim() || !this.position.trim()) {
      alert('Заполните имя и должность');
      return;
    }

    if (this.isEdit && this.participantId) {
      // PUT-запрос на обновление
      this.api.updateParticipant(this.participantId, {
        id: this.participantId,
        name: this.name,
        position: this.position
      }).subscribe(() => {
        this.router.navigate(['/participants']);
      });
    } else {
      this.api.createParticipant({
        name: this.name,
        position: this.position
      }).subscribe(() => {
        this.router.navigate(['/participants']);
      });
    }
  }
}