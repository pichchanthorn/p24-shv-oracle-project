import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-two-factor-setup',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './two-factor-setup.html',
  styleUrl: './two-factor-setup.css'
})
export class TwoFactorSetup implements OnInit {
  qrCodeDataUrl = signal<string | null>(null);
  secret = signal<string | null>(null);
  twoFactorCode = '';

  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);
  isLoading = signal(false);

  constructor(private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.loadSetup();
  }

  loadSetup(): void {
    this.isLoading.set(true);
    this.authService.setupTwoFactor().subscribe({
      next: (response) => {
        this.isLoading.set(false);
        this.qrCodeDataUrl.set(response.qrCodeDataUrl);
        this.secret.set(response.secret);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Failed to start 2FA setup. Please make sure you are logged in.');
      }
    });
  }

  onEnable(): void {
    this.errorMessage.set(null);
    this.isLoading.set(true);

    this.authService.enableTwoFactor(this.twoFactorCode).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.successMessage.set('Two-factor authentication enabled successfully!');
        setTimeout(() => this.router.navigate(['/dashboard']), 1500);
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Invalid verification code. Please try again.');
      }
    });
  }
}