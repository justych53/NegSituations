import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-failure-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './failure-list.html'
})
export class FailureListComponent implements OnInit {
  records: any[] = [];

  constructor(private api: ApiService, private auth: AuthService) {}

    get currentUser() {
    return this.auth.getUser();
  }

  get isAdmin() {
    return this.auth.isAdmin();
  }

  ngOnInit(): void {
    this.loadRecords();
  }

  loadRecords(): void {
    this.api.getFailureRecords().subscribe(data => {
      this.records = data;
      console.log('Записи с бекенда:', data);
    });
  }

  delete(id: number): void {
    if (confirm('Удалить запись?')) {
      this.api.deleteFailureRecord(id).subscribe(() => {
        this.loadRecords();
      });
    }
  }
}