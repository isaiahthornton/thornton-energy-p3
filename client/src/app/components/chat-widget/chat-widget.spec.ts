import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from '../../services/auth-service';
import { ChatService } from '../../services/chat-service';
import { SessionUser } from '../../interfaces/user';
import { ChatWidget } from './chat-widget';

describe('ChatWidget', () => {
  const user$ = new BehaviorSubject<SessionUser | null>(null);

  function render() {
    // A fake login state and a fake chat service, so no real API or AI is called
    TestBed.configureTestingModule({
      imports: [ChatWidget],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { currentUser$: user$.asObservable(), getCurrentUser: () => user$.value } },
        { provide: ChatService, useValue: { getFirstName: () => Promise.resolve('Jane'), stream: () => Promise.resolve() } },
      ],
    });
    const fixture = TestBed.createComponent(ChatWidget);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the button to visitors who are not signed in', () => {
    user$.next(null);
    const fixture = render();
    expect(fixture.nativeElement.querySelector('.chat-launcher')).not.toBeNull();
  });

  it('shows the button to an admin too (they get the general assistant)', () => {
    user$.next({ id: 1, email: 'admin@example.com', role: 'admin', customerId: null });
    const fixture = render();
    expect(fixture.nativeElement.querySelector('.chat-launcher')).not.toBeNull();
  });

  it('opens and closes the window when the button is clicked', () => {
    user$.next({ id: 2, email: 'jane@example.com', role: 'customer', customerId: 1 });
    const fixture = render();
    const panel = fixture.nativeElement.querySelector('.chat-panel') as HTMLElement;
    const button = fixture.nativeElement.querySelector('.chat-launcher') as HTMLButtonElement;

    expect(panel.hidden).toBe(true);

    button.click();
    fixture.detectChanges();
    expect(panel.hidden).toBe(false);

    button.click();
    fixture.detectChanges();
    expect(panel.hidden).toBe(true);
  });

  it('greets a visitor with the general welcome and a customer with the personal one', () => {
    user$.next(null);
    const visitor = render();
    expect(visitor.nativeElement.textContent).toContain('Sign in as a customer');

    TestBed.resetTestingModule();
    user$.next({ id: 2, email: 'jane@example.com', role: 'customer', customerId: 1 });
    const customer = render();
    expect(customer.nativeElement.textContent).toContain('bills, usage, meters');
  });
});
