import { Injectable, computed, signal } from '@angular/core';
import {
  ChatMessage,
  ChatRequest,
  ChatStreamChunk,
  ChatUiMessage,
  DashboardSnapshot
} from '../contracts/chat';
import { runtimeConfig } from '../core/runtime-config';

const MAX_CONTEXT_MESSAGES = 12;

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly messagesState = signal<ChatUiMessage[]>([]);
  private readonly errorState = signal<string | null>(null);
  private readonly isStreamingState = signal(false);
  private activeAbortController: AbortController | null = null;

  readonly messages = computed(() => this.messagesState());
  readonly isStreaming = computed(() => this.isStreamingState());
  readonly error = computed(() => this.errorState());

  async sendMessage(input: string, dashboardSnapshotId: string, dashboardSnapshot: DashboardSnapshot): Promise<void> {
    const content = input.trim();
    if (!content) {
      return;
    }

    this.errorState.set(null);
    this.activeAbortController?.abort();
    const contextMessages = this.toContextMessages();

    const userMessage: ChatUiMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content,
      createdAtIso: new Date().toISOString()
    };

    const assistantMessageId = crypto.randomUUID();
    const assistantMessage: ChatUiMessage = {
      id: assistantMessageId,
      role: 'assistant',
      content: '',
      createdAtIso: new Date().toISOString(),
      isStreaming: true
    };

    this.messagesState.update((messages) => [...messages, userMessage, assistantMessage]);
    this.isStreamingState.set(true);

    this.activeAbortController = new AbortController();
    const request: ChatRequest = {
      dashboardSnapshotId,
      dashboardSnapshot,
      messages: [...contextMessages, { role: 'user', content }]
    };

    try {
      await this.streamChatResponse(request, assistantMessageId, this.activeAbortController.signal);
      this.messagesState.update((messages) =>
        messages.map((message) =>
          message.id === assistantMessageId ? { ...message, isStreaming: false } : message
        )
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown chat error';
      this.errorState.set(message);
      this.messagesState.update((messages) =>
        messages.map((chatMessage) =>
          chatMessage.id === assistantMessageId
            ? {
                ...chatMessage,
                isStreaming: false,
                hasError: true,
                content: chatMessage.content || 'The assistant could not respond.'
              }
            : chatMessage
        )
      );
    } finally {
      this.activeAbortController = null;
      this.isStreamingState.set(false);
    }
  }

  cancelStreaming(): void {
    this.activeAbortController?.abort();
    this.activeAbortController = null;
    this.isStreamingState.set(false);
    this.messagesState.update((messages) =>
      messages.map((message) =>
        message.isStreaming ? { ...message, isStreaming: false, hasError: true } : message
      )
    );
  }

  private toContextMessages(): ChatMessage[] {
    const normalized = this.messagesState()
      .filter((message) => message.content.trim().length > 0)
      .map<ChatMessage>((message) => ({
        role: message.role,
        content: message.content
      }));

    return normalized.slice(-MAX_CONTEXT_MESSAGES);
  }

  private async streamChatResponse(
    request: ChatRequest,
    assistantMessageId: string,
    signal: AbortSignal
  ): Promise<void> {
    const response = await fetch(`${runtimeConfig.apiBaseUrl}/api/chat/stream`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'text/event-stream'
      },
      body: JSON.stringify(request),
      signal
    });

    if (!response.ok) {
      throw new Error(`Chat request failed (${response.status})`);
    }

    const stream = response.body;
    if (!stream) {
      throw new Error('No stream returned by API');
    }

    const reader = stream.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const result = await reader.read();
      if (result.done) {
        break;
      }

      buffer += decoder.decode(result.value, { stream: true });
      const events = buffer.split('\n\n');
      buffer = events.pop() ?? '';

      for (const eventChunk of events) {
        const payload = this.extractSseData(eventChunk);
        if (!payload) {
          continue;
        }

        const streamChunk = JSON.parse(payload) as ChatStreamChunk;
        this.applyStreamChunk(streamChunk, assistantMessageId);
      }
    }
  }

  private extractSseData(eventChunk: string): string | null {
    const lines = eventChunk
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.startsWith('data:'));

    if (lines.length === 0) {
      return null;
    }

    return lines.map((line) => line.replace(/^data:\s?/, '')).join('');
  }

  private applyStreamChunk(chunk: ChatStreamChunk, assistantMessageId: string): void {
    if (chunk.type === 'chunk' && chunk.delta) {
      this.messagesState.update((messages) =>
        messages.map((message) =>
          message.id === assistantMessageId
            ? { ...message, content: `${message.content}${chunk.delta}` }
            : message
        )
      );
      return;
    }

    if (chunk.type === 'error') {
      throw new Error(chunk.message ?? 'Provider returned an error chunk');
    }
  }
}
