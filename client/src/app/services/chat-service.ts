import { Injectable, inject } from '@angular/core';
import { AuthService } from './auth-service';
import { API_URL } from '../utils/api';

// The assistant's name. Change it here and in server/ThorntonEnergy.Chat.Api/ChatPersona.cs.
export const BOT_NAME = 'Wattson';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface OutageConfirmation {
  confirmed: true;
  address: string;
  description: string;
}

export interface OutageConfirmationPrompt {
  address: string;
  description: string;
}

export interface ChatStreamHandlers {
  onDelta: (text: string) => void;
  onConfirmationRequired: (request: OutageConfirmationPrompt) => void;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly auth = inject(AuthService);

  // The customer's first name for the greeting, or null if it cannot be loaded
  async getFirstName(): Promise<string | null> {
    const token = this.auth.getToken();
    if (!token) return null;
    try {
      const response = await fetch(`${API_URL}/chat/hello`, { headers: { Authorization: `Bearer ${token}` } });
      if (!response.ok) return null;
      const body = (await response.json()) as { firstName?: string | null };
      return body.firstName ?? null;
    } catch {
      return null;
    }
  }

  async stream(
    messages: ChatMessage[],
    handlers: ChatStreamHandlers,
    outageConfirmation?: OutageConfirmation,
    signal?: AbortSignal,
  ): Promise<void> {
    // Visitors have no token and get the general assistant; the server decides from the token's role
    const token = this.auth.getToken();

    const response = await fetch(`${API_URL}/chat/stream`, {
      method: 'POST',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        'Content-Type': 'application/json',
        Accept: 'text/event-stream',
      },
      body: JSON.stringify({ messages, outageConfirmation }),
      signal,
    });

    if (!response.ok || !response.body) {
      throw new Error(response.status === 503
        ? 'Chat is not configured yet. Please try again later.'
        : response.status === 429
          ? 'Lots of people are chatting right now. Please wait a minute and try again.'
          : 'Chat is temporarily unavailable. Please retry.');
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let eventName = 'message';
    let eventData: string[] = [];

    const dispatch = (): boolean => {
      if (eventData.length === 0) return false;
      const data = eventData.join('\n');
      eventData = [];
      try {
        if (eventName === 'delta') {
          const parsed = JSON.parse(data) as { text?: string };
          if (parsed.text) handlers.onDelta(parsed.text);
        } else if (eventName === 'confirmation_required') {
          handlers.onConfirmationRequired(JSON.parse(data) as OutageConfirmationPrompt);
          return true;
        } else if (eventName === 'error') {
          const parsed = JSON.parse(data) as { message?: string };
          throw new Error(parsed.message || 'Chat is temporarily unavailable. Please retry.');
        }
      } finally {
        eventName = 'message';
      }
      return false;
    };

    try {
      while (true) {
        const { value, done } = await reader.read();
        buffer += decoder.decode(value, { stream: !done });
        const lines = buffer.split(/\r?\n/);
        buffer = lines.pop() ?? '';
        for (const line of lines) {
          if (line === '') {
            if (dispatch()) {
              await reader.cancel();
              return;
            }
          } else if (line.startsWith('event:')) {
            eventName = line.slice(6).trim();
          } else if (line.startsWith('data:')) {
            eventData.push(line.slice(5).trimStart());
          }
        }
        if (done) {
          dispatch();
          return;
        }
      }
    } finally {
      reader.releaseLock();
    }
  }
}
