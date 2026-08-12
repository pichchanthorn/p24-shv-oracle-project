import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  requiresTwoFactor: boolean;
  accessToken: string | null;
  challengeToken: string | null;
  expiresAtUtc: string;
}

export interface TwoFactorSetupResponse {
  secret: string;
  otpAuthUri: string;
  qrCodeDataUrl: string;
}

export interface TwoFactorVerifyLoginRequest {
  challengeToken: string;
  twoFactorCode: string;
}

export interface UserProfile {
  username: string;
  email: string;
  isTwoFactorEnabled: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiBaseUrl = 'https://localhost:7195/api/auth';

  // Signal to track current access token (in-memory only)
  accessToken = signal<string | null>(null);

  constructor(private http: HttpClient) {}

  register(request: RegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiBaseUrl}/register`, request);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/login`, request).pipe(
      tap(response => {
        if (!response.requiresTwoFactor && response.accessToken) {
          this.accessToken.set(response.accessToken);
        }
      })
    );
  }

  setupTwoFactor(): Observable<TwoFactorSetupResponse> {
    return this.http.post<TwoFactorSetupResponse>(`${this.apiBaseUrl}/2fa/setup`, {});
  }

  enableTwoFactor(twoFactorCode: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiBaseUrl}/2fa/enable`, { twoFactorCode });
  }

  verifyTwoFactorLogin(request: TwoFactorVerifyLoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/2fa/verify-login`, request).pipe(
      tap(response => {
        if (response.accessToken) {
          this.accessToken.set(response.accessToken);
        }
      })
    );
  }

  disableTwoFactor(twoFactorCode: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiBaseUrl}/2fa/disable`, { twoFactorCode });
  }

  getCurrentUser(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.apiBaseUrl}/me`);
  }

  logout(): void {
    this.accessToken.set(null);
  }

  isLoggedIn(): boolean {
    return this.accessToken() !== null;
  }
}