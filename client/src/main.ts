import { bootstrapApplication } from '@angular/platform-browser';
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, provideHttpClient } from '@angular/common/http';

interface Customer { id: string; fullName: string; email: string; phone: string; }
interface Account { id: string; customerId: string; number: string; type: string; currency: string; balance: number; status: string; }
interface Transaction { id: string; amount: number; type: string; description: string; createdAt: string; }
interface Loan { id: string; principal: number; termMonths: number; status: string; createdAt: string; }
interface Notification { id: string; message: string; createdAt: string; }

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="app-shell">
      <aside class="sidebar">
        <div class="brand"><span class="mark">B</span><strong>Bankly</strong></div>
        <p class="label">WORKSPACE</p>
        <button *ngFor="let item of navigation" [class.active]="activeView === item.key" (click)="activeView = item.key">
          <span>{{ item.icon }}</span>{{ item.label }}
        </button>
        <div class="sidebar-bottom"><span class="status-dot" [class.offline]="status !== 'online'"></span> API {{ status }}</div>
      </aside>

      <main class="content">
        <header class="topbar"><div><p class="eyebrow">PERSONAL BANKING</p><h1>{{ pageTitle }}</h1></div><div class="profile">{{ customer?.fullName || 'Guest' }}<span>●</span></div></header>

        <section *ngIf="!customer" class="onboarding card">
          <div><p class="eyebrow">GET STARTED</p><h2>Open your banking workspace</h2><p>Create a customer profile to explore accounts, transfers, loans, and statements.</p></div>
          <form (ngSubmit)="register()" #registration="ngForm">
            <input [(ngModel)]="registrationForm.fullName" name="fullName" placeholder="Full name" required>
            <input [(ngModel)]="registrationForm.email" name="email" type="email" placeholder="Email address" required>
            <input [(ngModel)]="registrationForm.phone" name="phone" placeholder="Phone number">
            <input [(ngModel)]="registrationForm.password" name="password" type="password" placeholder="Password" required>
            <button class="primary" [disabled]="registration.invalid">Create profile</button>
          </form>
          <p class="error" *ngIf="error">{{ error }}</p>
        </section>

        <ng-container *ngIf="customer">
          <section *ngIf="activeView === 'overview'">
            <div class="welcome card"><div><p class="eyebrow">WELCOME BACK</p><h2>{{ customer.fullName.split(' ')[0] }}, your finances are in one view.</h2><p>Monitor your accounts and move money securely.</p></div><button class="primary" (click)="activeView = 'accounts'">View accounts →</button></div>
            <div class="stats"><div class="stat card"><span>Total balance</span><strong>{{ totalBalance | currency }}</strong><small>Across {{ accounts.length }} account(s)</small></div><div class="stat card"><span>Active loans</span><strong>{{ loans.length }}</strong><small>Applications and repayments</small></div><div class="stat card"><span>Notifications</span><strong>{{ notifications.length }}</strong><small>Latest account activity</small></div></div>
            <div class="section-heading"><h2>Recent activity</h2><button class="link" (click)="activeView = 'statements'">View statement</button></div>
            <div class="card table-card"><p *ngIf="!transactions.length" class="empty">No transactions yet. Deposit funds into an account to get started.</p><div class="transaction" *ngFor="let transaction of transactions"><span class="transaction-icon">{{ transaction.amount >= 0 ? '↓' : '↑' }}</span><div><strong>{{ transaction.description }}</strong><small>{{ transaction.createdAt | date:'medium' }}</small></div><b [class.positive]="transaction.amount >= 0">{{ transaction.amount | currency }}</b></div></div>
          </section>

          <section *ngIf="activeView === 'accounts'">
            <div class="section-heading"><div><p class="eyebrow">YOUR MONEY</p><h2>Accounts</h2></div><button class="primary" (click)="showAccountForm = !showAccountForm">+ New account</button></div>
            <form *ngIf="showAccountForm" class="card inline-form" (ngSubmit)="createAccount()"><select [(ngModel)]="accountForm.type" name="type"><option>Savings</option><option>Current</option></select><select [(ngModel)]="accountForm.currency" name="currency"><option>INR</option><option>USD</option></select><button class="primary">Create account</button></form>
            <div class="account-grid"><div class="account-card" *ngFor="let account of accounts"><div class="account-top"><span class="account-type">{{ account.type }}</span><span class="pill">{{ account.status }}</span></div><p class="account-number">•••• {{ account.number.slice(-4) }}</p><strong>{{ account.balance | currency:account.currency }}</strong><div class="account-actions"><button (click)="selectedAccount = account; showDepositForm = true">Deposit</button><button (click)="loadTransactions(account)">Activity</button></div></div></div>
            <form *ngIf="showDepositForm && selectedAccount" class="card inline-form" (ngSubmit)="deposit()"><strong>Deposit into {{ selectedAccount.type }}</strong><input [(ngModel)]="depositAmount" name="amount" type="number" min="1" placeholder="Amount" required><button class="primary">Deposit funds</button><button type="button" class="link" (click)="showDepositForm = false">Cancel</button></form>
          </section>

          <section *ngIf="activeView === 'transfers'">
            <div class="section-heading"><div><p class="eyebrow">MOVE MONEY</p><h2>Transfers & payments</h2></div></div>
            <form class="card form-card" (ngSubmit)="transfer()"><label>From account<select [(ngModel)]="transferForm.fromAccountId" name="from" required><option value="">Select account</option><option *ngFor="let account of accounts" [value]="account.id">{{ account.type }} · {{ account.number }} ({{ account.balance | currency }})</option></select></label><label>To account<select [(ngModel)]="transferForm.toAccountId" name="to" required><option value="">Select account</option><option *ngFor="let account of accounts" [value]="account.id">{{ account.type }} · {{ account.number }}</option></select></label><label>Amount<input [(ngModel)]="transferForm.amount" name="amount" type="number" min="1" required></label><label>Description<input [(ngModel)]="transferForm.description" name="description" placeholder="What is this for?"></label><button class="primary">Send transfer</button><p class="success" *ngIf="message">{{ message }}</p><p class="error" *ngIf="error">{{ error }}</p></form>
          </section>

          <section *ngIf="activeView === 'loans'"><div class="section-heading"><div><p class="eyebrow">BORROWING</p><h2>Loans</h2></div><button class="primary" (click)="showLoanForm = !showLoanForm">+ Apply for loan</button></div><form *ngIf="showLoanForm" class="card inline-form" (ngSubmit)="applyLoan()"><input [(ngModel)]="loanForm.amount" name="amount" type="number" min="1" placeholder="Loan amount" required><input [(ngModel)]="loanForm.termMonths" name="term" type="number" min="1" placeholder="Term in months" required><button class="primary">Submit application</button></form><div class="card table-card"><p *ngIf="!loans.length" class="empty">No loan applications yet.</p><div class="transaction" *ngFor="let loan of loans"><div><strong>{{ loan.principal | currency }} loan</strong><small>{{ loan.termMonths }} months · {{ loan.createdAt | date:'mediumDate' }}</small></div><span class="pill">{{ loan.status }}</span></div></div></section>

          <section *ngIf="activeView === 'statements'"><div class="section-heading"><div><p class="eyebrow">REPORTING</p><h2>Statement</h2></div><button class="primary" (click)="loadStatement()">Refresh statement</button></div><div class="card table-card"><div class="transaction" *ngFor="let transaction of transactions"><div><strong>{{ transaction.description }}</strong><small>{{ transaction.createdAt | date:'medium' }}</small></div><b [class.positive]="transaction.amount >= 0">{{ transaction.amount | currency }}</b></div><p *ngIf="!transactions.length" class="empty">Your statement is empty.</p></div></section>

          <section *ngIf="activeView === 'notifications'"><div class="section-heading"><div><p class="eyebrow">ALERTS</p><h2>Notifications</h2></div></div><div class="card table-card"><div class="transaction" *ngFor="let notification of notifications"><span class="notification-icon">!</span><div><strong>{{ notification.message }}</strong><small>{{ notification.createdAt | date:'medium' }}</small></div></div><p *ngIf="!notifications.length" class="empty">You are all caught up.</p></div></section>
        </ng-container>
      </main>
    </div>
  `,
  styles: [`
    :host { --ink:#14243d; --muted:#75839a; --line:#e4eaf2; --blue:#2563eb; --light:#f5f8fc; display:block; min-height:100vh; color:var(--ink); font-family:Inter,Arial,sans-serif; background:var(--light); } * { box-sizing:border-box; } button,input,select { font:inherit; } button { cursor:pointer; border:0; } .app-shell { display:flex; min-height:100vh; } .sidebar { width:240px; padding:30px 18px; background:#fff; border-right:1px solid var(--line); display:flex; flex-direction:column; gap:8px; } .brand { display:flex; align-items:center; gap:10px; font-size:21px; margin:0 12px 40px; } .mark { display:grid; place-items:center; width:36px; height:36px; border-radius:11px; color:#fff; background:var(--blue); } .label,.eyebrow { color:var(--blue); font-size:11px; font-weight:800; letter-spacing:1.2px; } .label { margin:0 12px 8px; color:#a0aabb; } .sidebar button { text-align:left; color:#71809a; background:transparent; padding:13px 12px; border-radius:10px; } .sidebar button span { display:inline-block; width:28px; } .sidebar button.active,.sidebar button:hover { color:var(--blue); background:#edf4ff; font-weight:700; } .sidebar-bottom { margin-top:auto; color:var(--muted); font-size:12px; padding:12px; }.status-dot { display:inline-block; width:8px; height:8px; border-radius:50%; background:#20b26b; margin-right:6px; }.status-dot.offline { background:#e05252; } .content { width:min(1200px,100%); padding:42px 52px; margin:0 auto; }.topbar { display:flex; justify-content:space-between; align-items:start; margin-bottom:34px; }.topbar h1 { margin:6px 0 0; font-size:32px; }.profile { color:#53627a; font-size:14px; }.profile span { color:var(--blue); margin-left:12px; }.card { background:#fff; border:1px solid var(--line); border-radius:16px; padding:28px; box-shadow:0 10px 24px #192e4b08; }.welcome { display:flex; justify-content:space-between; align-items:center; background:linear-gradient(115deg,#eaf3ff,#fff); }.welcome h2,.onboarding h2 { font-size:29px; margin:8px 0; }.welcome p:not(.eyebrow),.onboarding p:not(.eyebrow) { color:var(--muted); }.primary { background:var(--blue); color:white; padding:12px 18px; border-radius:9px; font-weight:700; }.primary:disabled { opacity:.5; cursor:not-allowed; }.stats { display:grid; grid-template-columns:repeat(3,1fr); gap:18px; margin:20px 0 34px; }.stat span,.stat small { display:block; color:var(--muted); }.stat strong { display:block; font-size:27px; margin:12px 0 7px; }.section-heading { display:flex; justify-content:space-between; align-items:center; margin:24px 0 16px; }.section-heading h2 { margin:5px 0; }.link { color:var(--blue); background:none; font-weight:700; }.table-card { padding:8px 24px; }.transaction { display:flex; align-items:center; gap:14px; padding:16px 0; border-bottom:1px solid var(--line); }.transaction:last-child { border-bottom:0; }.transaction > div:nth-child(2),.transaction > div:first-child:not(.notification-icon) { flex:1; }.transaction small { display:block; color:var(--muted); margin-top:5px; }.transaction b { color:#d44b4b; }.transaction b.positive { color:#17945d; }.transaction-icon,.notification-icon { display:grid; place-items:center; width:36px; height:36px; border-radius:10px; color:var(--blue); background:#edf4ff; font-weight:700; }.notification-icon { color:#c17c13; background:#fff5df; }.pill { background:#e8f7ef; color:#188451; padding:5px 9px; border-radius:20px; font-size:11px; text-transform:capitalize; }.account-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:18px; }.account-card { padding:23px; color:#fff; border-radius:16px; background:linear-gradient(135deg,#193a70,#286bd2); }.account-card:nth-child(2n) { background:linear-gradient(135deg,#42236c,#8b4fc7); }.account-top,.account-actions { display:flex; justify-content:space-between; align-items:center; }.account-type { font-weight:700; }.account-card .pill { color:#fff; background:#ffffff2e; }.account-number { opacity:.8; letter-spacing:2px; margin:30px 0 8px; }.account-card strong { font-size:25px; }.account-actions { margin-top:24px; border-top:1px solid #ffffff3d; padding-top:14px; }.account-actions button { background:none; color:#fff; font-size:12px; }.onboarding { display:grid; grid-template-columns:1fr 1fr; gap:30px; align-items:center; max-width:900px; margin:40px auto; }.onboarding form,.form-card { display:grid; gap:14px; }.onboarding input,.inline-form input,.inline-form select,.form-card input,.form-card select { width:100%; padding:12px 13px; border:1px solid var(--line); border-radius:8px; outline:none; }.form-card { max-width:620px; }.form-card label { display:grid; gap:6px; color:var(--muted); font-size:13px; font-weight:600; }.inline-form { display:flex; align-items:center; gap:12px; margin-bottom:18px; }.inline-form strong { margin-right:auto; }.empty { text-align:center; color:var(--muted); padding:30px; }.success { color:#188451; }.error { color:#d44b4b; } @media(max-width:800px) { .sidebar { width:70px; padding:20px 8px; }.brand strong,.sidebar button:not(.active)::after,.sidebar button { font-size:0; }.sidebar button span { font-size:18px; }.label,.sidebar-bottom { display:none; }.content { padding:25px 18px; }.stats,.onboarding { grid-template-columns:1fr; }.welcome { display:block; }.welcome .primary { margin-top:20px; } }
  `]
})
class AppComponent {
  private readonly http = inject(HttpClient);
  readonly navigation = [{ key: 'overview', label: 'Overview', icon: '⌂' }, { key: 'accounts', label: 'Accounts', icon: '▣' }, { key: 'transfers', label: 'Transfers', icon: '↗' }, { key: 'loans', label: 'Loans', icon: '◈' }, { key: 'statements', label: 'Statements', icon: '▤' }, { key: 'notifications', label: 'Notifications', icon: '◉' }];
  activeView = 'overview'; status = 'checking...'; pageTitle = 'Good morning'; customer?: Customer; accounts: Account[] = []; transactions: Transaction[] = []; loans: Loan[] = []; notifications: Notification[] = []; error = ''; message = ''; showAccountForm = false; showDepositForm = false; showLoanForm = false; selectedAccount?: Account; depositAmount = 0;
  registrationForm = { fullName: '', email: '', phone: '', password: '' }; accountForm = { type: 'Savings', currency: 'INR' }; loanForm = { amount: 0, termMonths: 12 }; transferForm = { fromAccountId: '', toAccountId: '', amount: 0, description: '' };
  constructor() { this.http.get('/health', { responseType: 'text' }).subscribe({ next: () => this.status = 'online', error: () => this.status = 'offline' }); }
  get totalBalance() { return this.accounts.reduce((total, account) => total + account.balance, 0); }
  register() { this.error = ''; this.http.post<Customer>('/api/identity/register', this.registrationForm).subscribe({ next: customer => { this.customer = customer; this.loadCustomerData(); }, error: () => this.error = 'Could not create the profile. Check that the API is running.' }); }
  loadCustomerData() { if (!this.customer) return; this.http.get<Account[]>(`/api/accounts/customer/${this.customer.id}`).subscribe(accounts => { this.accounts = accounts; this.loadStatement(); }); this.http.get<Loan[]>(`/api/loans/customer/${this.customer.id}`).subscribe(loans => this.loans = loans); this.http.get<Notification[]>(`/api/notifications/customer/${this.customer.id}`).subscribe(notifications => this.notifications = notifications); }
  createAccount() { if (!this.customer) return; this.http.post<Account>('/api/accounts', { customerId: this.customer.id, ...this.accountForm }).subscribe(account => { this.accounts = [...this.accounts, account]; this.showAccountForm = false; }); }
  deposit() { if (!this.selectedAccount) return; this.http.post<Account>(`/api/accounts/${this.selectedAccount.id}/deposit`, { amount: this.depositAmount }).subscribe(account => { this.accounts = this.accounts.map(item => item.id === account.id ? account : item); this.selectedAccount = account; this.showDepositForm = false; this.loadStatement(); }); }
  loadTransactions(account: Account) { this.http.get<Transaction[]>(`/api/transactions/account/${account.id}`).subscribe(transactions => this.transactions = transactions); }
  transfer() { this.error = ''; this.message = ''; this.http.post<{ message: string }>('/api/transactions/transfer', this.transferForm).subscribe({ next: result => { this.message = result.message; this.transferForm.amount = 0; this.loadCustomerData(); }, error: response => this.error = response.error || 'The transfer could not be completed.' }); }
  applyLoan() { if (!this.customer) return; this.http.post<Loan>('/api/loans', { customerId: this.customer.id, ...this.loanForm }).subscribe(loan => { this.loans = [loan, ...this.loans]; this.showLoanForm = false; }); }
  loadStatement() { if (!this.customer) return; this.http.get<{ transactions: Transaction[] }>(`/api/reports/customer/${this.customer.id}/statement`).subscribe(statement => this.transactions = statement.transactions); }
}

bootstrapApplication(AppComponent, { providers: [provideHttpClient()] });
