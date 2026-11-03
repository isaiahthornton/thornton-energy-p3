import { Component, inject, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ChatMessage,
  ChatService,
  OutageConfirmation,
  OutageConfirmationPrompt,
} from '../../../services/chat-service';

@Component({
  imports: [FormsModule],
  selector: 'app-customer-chat',
  styleUrl: './customer-chat.css',
  templateUrl: './customer-chat.html',
})
export class CustomerChat implements OnDestroy {
  private readonly chat = inject(ChatService);
  protected readonly messages = signal<ChatMessage[]>([
    {
      role: 'assistant',
      content: 'Hello! I can help with your account, bills, meter readings, and outage reports. What can I help you with?',
    },
  ]);
  protected readonly draft = signal('');
  protected readonly isSending = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly confirmation = signal<OutageConfirmationPrompt | null>(null);

  private lastUserMessage = '';
  private abortController?: AbortController;

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
        content: `I confirm: report an outage at ${requested.address}. Details: ${requested.description}`,
      },
      { role: 'assistant', content: '' },
    ]);
    await this.submit({
      confirmed: true,
      address: requested.address,
      description: requested.description,
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
    this.messages.set([
      {
        role: 'assistant',
        content: 'Hello! I can help with your account, bills, meter readings, and outage reports. What can I help you with?',
      },
    ]);
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
