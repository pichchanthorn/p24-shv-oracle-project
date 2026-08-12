import { Routes } from '@angular/router';
import { Register } from './pages/register/register';
import { Login } from './pages/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { TwoFactorSetup } from './pages/two-factor-setup/two-factor-setup';
import { TwoFactorVerify } from './pages/two-factor-verify/two-factor-verify';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'register', component: Register },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard },
  { path: 'two-factor-setup', component: TwoFactorSetup },
  { path: 'two-factor-verify', component: TwoFactorVerify },
];