import { Routes } from '@angular/router';
import { Register } from './pages/register/register';
import { Login } from './pages/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { TwoFactorSetup } from './pages/two-factor-setup/two-factor-setup';
import { TwoFactorVerify } from './pages/two-factor-verify/two-factor-verify';
import { authGuard } from './services/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'register', component: Register },
  { path: 'login', component: Login },
  // two-factor-verify runs with only a short-lived challenge token (held in
  // navigation state, not an access token), so it is intentionally outside
  // the auth guard.
  { path: 'two-factor-verify', component: TwoFactorVerify },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: 'two-factor-setup', component: TwoFactorSetup, canActivate: [authGuard] },
];