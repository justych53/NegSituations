import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-panel.html'
})
export class AdminPanelComponent implements OnInit {
  users: any[] = [];
  newUsername = '';
  newPassword = '';
  error = '';

  constructor(private api: ApiService, public auth: AuthService) {}

  ngOnInit(): void {
    if (!this.auth.isAdmin()) {
      return;
    }
    this.loadUsers();
  }

  loadUsers(): void {
    this.api.getUsers().subscribe({
      next: (data) => (this.users = data),
      error: (err) => (this.error = 'Ошибка загрузки пользователей')
    });
  }

  createUser(): void {
    if (!this.newUsername || !this.newPassword) {
      this.error = 'Заполните имя и пароль';
      return;
    }
    this.api.createUser(this.newUsername, this.newPassword).subscribe({
      next: () => {
        this.newUsername = '';
        this.newPassword = '';
        this.error = '';
        this.loadUsers();
      },
      error: (err) => (this.error = 'Ошибка создания')
    });
  }

  deleteUser(id: number): void {
    if (confirm('Удалить эксперта?')) {
      this.api.deleteUser(id).subscribe(() => this.loadUsers());
    }
  }
}