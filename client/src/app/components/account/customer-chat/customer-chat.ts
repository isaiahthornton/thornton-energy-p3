import { Component, inject, OnDestroy, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../services/auth-service';
import {
  BOT_NAME,
  ChatMessage,
  ChatService,
  OutageConfirmation,
  OutageConfirmationPrompt,
} from '../../../services/chat-service';

// The chat window itself. The floating button and open/close live in ChatWidget.
@Component({
  imports: [FormsModule],
  selector: 'app-customer-chat',
  styleUrl: './customer-chat.css',
  templateUrl: './customer-chat.html',
})
export class CustomerChat implements OnInit, OnDestroy {
  private readonly chat = inject(ChatService);
  private readonly isCustomer = inject(AuthService).getCurrentUser()?.role === 'customer';

  // Tells the widget to close the window
  readonly closed = output<void>();

  protected readonly botName = BOT_NAME;

  // Customers get the personal assistant (with their first name once it loads); everyone else the general one
  private greeting = this.isCustomer
    ? `Hi there! I'm ${BOT_NAME}. I can help with your bills, usage, meters, outages and any energy questions. What can I do for you?`
    : `Hi! I'm ${BOT_NAME}, Thornton Energy's virtual assistant. Ask me about our rate plans, saving energy, or what to do in an outage. Sign in as a customer to ask about your own account.`;
  protected readonly messages = signal<ChatMessage[]>([{ role: 'assistant', content: this.greeting }]);
  protected readonly draft = signal('');
  protected readonly isSending = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly confirmation = signal<OutageConfirmationPrompt | null>(null);

  private lastUserMessage = '';
  private abortController?: AbortController;

  async ngOnInit(): Promise<void> {
    // Say "Hi <first name>" when the window is created (a cheap lookup, no AI call)
    if (!this.isCustomer) return;
    const firstName = await this.chat.getFirstName();
    if (!firstName) return;
    this.greeting = `Hi ${firstName}! I'm ${BOT_NAME}. I can help with your bills, usage, meters, outages and any energy questions. What can I do for you?`;
    // Only swap the greeting if the customer has not started typing yet
    if (this.messages().length === 1) this.messages.set([{ role: 'assistant', content: this.greeting }]);
  }

  ngOnDestroy(): void {
    this.abortController?.abort();
    this.messages.set([]);
  }

  protected async send(): Promise<void> {
    const content = this.draft().trim();
    if (!content || this.isSending()) return;
    this.draft.set('');
    this.lastUserMessage = content;
    this.messages.update((items) => [...items, { role: 'user', content }, { role: 'assistant', content: '' }]);
    await this.submit();
  }

  // Enter sends, Shift+Enter makes a new line
  protected onEnter(event: Event): void {
    const key = event as KeyboardEvent;
    if (key.shiftKey) return;
    key.preventDefault();
    void this.send();
  }

  protected async retry(): Promise<void> {
    if (this.isSending() || !this.lastUserMessage) return;
    this.errorMessage.set('');
    this.messages.update((items) => {
      const next = [...items];
      // Discard any partial assistant output from the failed attempt before retrying.
      if (next.at(-1)?.role === 'assistant') next.pop();
      next.push({ role: 'assistant', content: '' });
      return next;
    });
    await this.submit();
  }

  protected async confirmOutage(): Promise<void> {
    const requested = this.confirmation();
    if (!requested || this.isSending()) return;
    this.confirmation.set(null);
    this.messages.update((items) => [
      ...items,
      {
        role: 'user',
        content: 'Yes, send it.',
      },
      { role: 'assistant', content: '' },
    ]);
    await this.submit({
      confirmed: true,
      address: requested.address,
      description: requested.description,
    });
  }

  protected cancelOutage(): void {
    this.confirmation.set(null);
    this.messages.update((items) => {
      // Drop the empty assistant bubble left by the paused answer, then say nothing was sent
      const kept = items.at(-1)?.content === '' ? items.slice(0, -1) : items;
      return [...kept, { role: 'assistant', content: 'No problem, I have not sent a report. Is there anything else I can help with?' }];
    });
  }

  protected reset(): void {
    this.abortController?.abort();
    this.abortController = undefined;
    this.isSending.set(false);
    this.confirmation.set(null);
    this.errorMessage.set('');
    this.draft.set('');
    this.lastUserMessage = '';
    this.messages.set([{ role: 'assistant', content: this.greeting }]);
  }

  protected renderMarkdown(source: string): string {
    const escape = (text: string) =>
      text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#39;');
    let html = escape(source);
    html = html.replace(/`([^`\n]+)`/g, '<code>$1</code>');
    html = html.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/\*([^*\n]+)\*/g, '<em>$1</em>');
    html = html.replace(/^### (.+)$/gm, '<h4>$1</h4>');
    html = html.replace(/^## (.+)$/gm, '<h3>$1</h3>');
    html = html.replace(/^# (.+)$/gm, '<h2>$1</h2>');
    html = html.replace(/^(\d+)\. (.+)$/gm, '<span class="numbered-choice"><b>$1.</b> $2</span>');
    html = html.replace(/^- (.+)$/gm, '<span class="bullet-choice">• $1</span>');
    return html.replace(/\n/g, '<br>');
  }

  private async submit(outageConfirmation?: OutageConfirmation): Promise<void> {
    this.isSending.set(true);
    this.errorMessage.set('');
    const assistantIndex = this.messages().length - 1;
    this.abortController = new AbortController();
    try {
      await this.chat.stream(
        this.messages().filter((message) => message.content.trim().length > 0),
        {
          onDelta: (text) => this.messages.update((items) => {
            const next = [...items];
            const current = next[assistantIndex];
            if (current?.role === 'assistant') next[assistantIndex] = { ...current, content: current.content + text };
            return next;
          }),
          onConfirmationRequired: (request) => this.confirmation.set(request),
        },
        outageConfirmation,
        this.abortController.signal,
      );
    } catch (error) {
      if (!(error instanceof DOMException && error.name === 'AbortError')) {
        this.errorMessage.set(error instanceof Error ? error.message : 'Chat is temporarily unavailable. Please retry.');
      }
    } finally {
      this.isSending.set(false);
      this.abortController = undefined;
    }
  }
}
