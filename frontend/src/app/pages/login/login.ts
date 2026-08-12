import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  username = '';
  password = '';

  errorMessage = signal<string | null>(null);
  isLoading = signal(false);

  constructor(private authService: AuthService, private router: Router) {}

  onSubmit(): void {
    this.errorMessage.set(null);
    this.isLoading.set(true);

    this.authService.login({
      username: this.username,
      password: this.password
    }).subscribe({
      next: (response) => {
        this.isLoading.set(false);

        if (response.requiresTwoFactor && response.challengeToken) {
          // Navigate to 2FA verify page, passing the challenge token
          this.router.navigate(['/two-factor-verify'], {
            state: { challengeToken: response.challengeToken }
          });
        } else {
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 423) {
          this.errorMessage.set('Account is temporarily locked. Try again later.');
        } else if (err.status === 401) {
          this.errorMessage.set('Invalid username or password.');
        } else {
          this.errorMessage.set('Login failed. Please try again.');
        }
      }
    });
  }
}