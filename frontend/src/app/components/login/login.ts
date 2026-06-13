import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './login.html'
})
export class LoginComponent implements OnInit {
  username = '';
  password = '';
  error = '';
  returnUrl: string | null = null;

  constructor(
    private auth: AuthService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

ngOnInit(): void {
  this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || null;
  // Если уже авторизован – сразу на failures
  if (this.auth.isLoggedIn()) {
    this.router.navigate([this.returnUrl || '/failures']);
  }
}

  login(): void {
    this.error = '';
    this.auth.login(this.username, this.password).subscribe({
      next: () => {
        const target = this.returnUrl || '/failures';
        this.router.navigate([target]);
      },
      error: () => {
        this.error = 'Неверное имя пользователя или пароль';
      }
    });
  }
}