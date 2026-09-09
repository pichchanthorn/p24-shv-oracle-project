import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth';

// Only attach the access token to requests aimed at our own backend API.
// This prevents leaking the token to third-party URLs a request might
// target (e.g. an external image or webhook URL passed through as data).
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.accessToken();

  const isBackendApiRequest = req.url.startsWith(authService.apiBaseUrl);

  const request =
    token && isBackendApiRequest
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      // A 401 from our own API on an authenticated request means the token
      // is missing/expired/invalid — clear it and send the user back to
      // login rather than leaving the app in a half-authenticated state.
      if (
        isBackendApiRequest &&
        token &&
        error instanceof HttpErrorResponse &&
        error.status === 401
      ) {
        authService.logout();
        router.navigate(['/login']);
      }
      return throwError(() => error);
    })
  );
};
