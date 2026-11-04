import { Routes } from '@angular/router';
import { Landing } from './components/landing/landing';
import { Login } from './components/login/login';
import { About } from './components/about/about';
import { Contact } from './components/contact/contact';
import { Register } from './components/register/register';
import { authGuard } from './guards/auth-guard';

export const routes: Routes = [
  { path: '', component: Landing, title: 'Thornton Energy' },
  { path: 'login', component: Login, title: 'Login | Thornton Energy' },
  { path: 'about', component: About, title: 'About | Thornton Energy' },
  { path: 'contact', component: Contact, title: 'Contact | Thornton Energy' },
  { path: 'register', component: Register, title: 'Register | Thornton Energy' },
  {
    path: 'account',
    loadComponent: () => import('./components/account/account-layout/account-layout').then((m) => m.AccountLayout),
    canActivate: [authGuard],
    data: { role: 'customer' },
    children: [
      {
        path:'',
        loadComponent: () => import('./components/account/account-dashboard/account-dashboard').then((m) => m.AccountDashboard),
        title: 'My Account | Thornton Energy',
      },
      {
        path: 'usage',
        loadComponent: () =>
          import('./components/account/usage-history/usage-history').then((m) => m.UsageHistory),
        title: 'Usage | Thornton Energy',
      },
      {
        path: 'billing',
        loadComponent: () =>
          import('./components/account/billing-history/billing-history').then((m) => m.BillingHistory),
        title: 'Billing | Thornton Energy',
      },
      {
        path: 'billing/:id',
        loadComponent: () =>
          import('./components/account/bill-detail/bill-detail').then((m) => m.BillDetail),
        title: 'Bill Details | Thornton Energy',
      },
      {
        path: 'outages',
        loadComponent: () =>
          import('./components/account/customer-outages/customer-outages').then((m) => m.CustomerOutages),
        title: 'Outages | Thornton Energy',
      },
      {
        path: 'rate-plans',
        loadComponent: () =>
          import('./components/account/rate-plan-compare/rate-plan-compare').then((m) => m.RatePlanCompare),
        title: 'Rate Plans | Thornton Energy',
      },
    ]
  },
  {
    path: 'admin',
    loadComponent: () => import('./components/admin/admin-layout/admin-layout').then((m) => m.AdminLayout),
    canActivate: [authGuard],
    data: { role: 'admin' },
    children: [
      {
        path: '',
        loadComponent: () => import('./components/admin/admin-dashboard/admin-dashboard').then((m) => m.AdminDashboard),
        title: 'Admin Dashboard | Thornton Energy',
      },
      {
        path: 'customers',
        loadComponent: () =>
          import('./components/admin/admin-customers/admin-customers').then((m) => m.AdminCustomers),
        title: 'Customers | Thornton Energy Admin',
      },
      {
        path: 'outages',
        loadComponent: () => import('./components/admin/admin-outages/admin-outages').then((m) => m.AdminOutages),
        title: 'Outage Queue | Thornton Energy Admin',
      },
      {
        path: 'outage-map',
        loadComponent: () => import('./components/admin/admin-outage-map/admin-outage-map').then((m) => m.AdminOutageMap),
        title: 'Outage Map | Thornton Energy Admin',
      },
      {
        path: 'rate-plans',
        loadComponent: () => import('./components/admin/admin-rate-plans/admin-rate-plans').then((m) => m.AdminRatePlans),
        title: 'Rate Plans | Thornton Energy Admin',
      },
      {
        path: 'meters',
        loadComponent: () => import('./components/admin/admin-meters/admin-meters').then((m) => m.AdminMeters),
        title: 'Meters | Thornton Energy Admin',
      },
      {
        path: 'billing',
        loadComponent: () => import('./components/admin/admin-billing/admin-billing').then((m) => m.AdminBilling),
        title: 'Billing | Thornton Energy Admin',
      },
      {
        path: 'messages',
        loadComponent: () => import('./components/admin/admin-messages/admin-messages').then((m) => m.AdminMessages),
        title: 'Inbox | Thornton Energy Admin',
      },
      {
        path: 'activity',
        loadComponent: () => import('./components/admin/admin-activity/admin-activity').then((m) => m.AdminActivity),
        title: 'Activity | Thornton Energy Admin',
      },
    ]
  },
  {
    path: '**',
    loadComponent: () =>
      import('./components/not-found/not-found').then((m) => m.NotFound),
    title: '404: Page Not Found | Thornton Energy',
  }
];
