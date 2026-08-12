import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-two-factor-verify',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './two-factor-verify.html',
  styleUrl: './two-factor-verify.css'
})
export class TwoFactorVerify implements OnInit {
  challengeToken = '';
  twoFactorCode = '';

  errorMessage = signal<string | null>(null);
  isLoading = signal(false);

  constructor(private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    const navigation = history.state;
    if (navigation && navigation['challengeToken']) {
      this.challengeToken = navigation['challengeToken'];
    } else {
      // No challenge token available, redirect back to login
      this.router.navigate(['/login']);
    }
  }

  onSubmit(): void {
    this.errorMessage.set(null);
    this.isLoading.set(true);

    this.authService.verifyTwoFactorLogin({
      challengeToken: this.challengeToken,
      twoFactorCode: this.twoFactorCode
    }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Invalid or expired code. Please try again.');
      }
    });
  }
}