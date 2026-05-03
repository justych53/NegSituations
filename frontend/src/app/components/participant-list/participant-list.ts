import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-participant-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './participant-list.html'
})
export class ParticipantListComponent implements OnInit {
  participants: any[] = [];

  constructor(private api: ApiService) {}

  ngOnInit(): void {
    this.loadParticipants();
  }

  loadParticipants(): void {
    this.api.getParticipants().subscribe(data => {
      this.participants = data;
    });
  }

  delete(id: number): void {
    if (confirm('Удалить участника?')) {
      this.api.deleteParticipant(id).subscribe(() => {
        this.loadParticipants();
      });
    }
  }
}