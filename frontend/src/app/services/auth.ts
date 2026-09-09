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

const ACCESS_TOKEN_STORAGE_KEY = 'access_token';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  readonly apiBaseUrl = 'https://localhost:7195/api/auth';

  // Access token is persisted in sessionStorage (cleared when the tab
  // closes) so a page refresh doesn't force the user to log in again.
  // Nothing else — no password, no TOTP secret — is ever stored client-side.
  accessToken = signal<string | null>(this.readStoredToken());

  constructor(private http: HttpClient) {}

  private readStoredToken(): string | null {
    try {
      return sessionStorage.getItem(ACCESS_TOKEN_STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private saveAccessToken(token: string): void {
    this.accessToken.set(token);
    try {
      sessionStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, token);
    } catch {
      // sessionStorage unavailable (e.g. private browsing) — token stays in-memory only.
    }
  }

  register(request: RegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiBaseUrl}/register`, request);
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/login`, request).pipe(
      tap(response => {
        if (!response.requiresTwoFactor && response.accessToken) {
          this.saveAccessToken(response.accessToken);
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
          this.saveAccessToken(response.accessToken);
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
    try {
      sessionStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
    } catch {
      // Nothing to clean up if sessionStorage isn't available.
    }
  }

  isLoggedIn(): boolean {
    return this.accessToken() !== null;
  }
}