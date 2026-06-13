import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isLoggedIn()) {
    return true;
  }

  // Перенаправляем на логин, запоминая исходный URL
  const returnUrl = state.url !== '/login' ? state.url : '/failures';
  router.navigate(['/login'], { queryParams: { returnUrl } });
  return false;
};