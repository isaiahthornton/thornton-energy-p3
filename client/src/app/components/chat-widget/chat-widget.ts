import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AuthService } from '../../services/auth-service';
import { BOT_NAME } from '../../services/chat-service';
import { CustomerChat } from '../account/customer-chat/customer-chat';

// The small button in the bottom-right corner, shown to everyone.
// Signing in or out starts a fresh conversation, so one person's chat never carries over to the next.
@Component({
  imports: [CustomerChat],
  selector: 'app-chat-widget',
  styleUrl: './chat-widget.css',
  templateUrl: './chat-widget.html',
})
export class ChatWidget {
  private readonly auth = inject(AuthService);

  protected readonly currentUser = toSignal(this.auth.currentUser$, { initialValue: null });
  protected readonly isOpen = signal(false);
  protected readonly botName = BOT_NAME;
  protected readonly chatKey = computed(() => String(this.currentUser()?.id ?? 'visitor'));

  protected toggle(): void {
    this.isOpen.update((open) => !open);
  }
}
