import { CommonModule, CurrencyPipe, PercentPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from './services/chat.service';
import { DashboardDataService } from './services/dashboard-data.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, CurrencyPipe, PercentPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly dashboardDataService = inject(DashboardDataService);
  private readonly chatService = inject(ChatService);

  protected readonly dashboardKpis = this.dashboardDataService.kpis;
  protected readonly regionRows = this.dashboardDataService.regions;
  protected readonly dashboardSnapshot = this.dashboardDataService.snapshot;
  protected readonly dashboardSnapshotId = this.dashboardDataService.snapshotId;
  protected readonly chatMessages = this.chatService.messages;
  protected readonly isStreaming = this.chatService.isStreaming;
  protected readonly chatError = this.chatService.error;
  protected readonly promptInput = signal('');
  protected readonly canSend = computed(() => this.promptInput().trim().length > 0 && !this.isStreaming());

  protected async sendPrompt(): Promise<void> {
    const prompt = this.promptInput().trim();
    if (!prompt) {
      return;
    }

    this.promptInput.set('');
    await this.chatService.sendMessage(prompt, this.dashboardSnapshotId(), this.dashboardSnapshot());
  }

  protected stopStreaming(): void {
    this.chatService.cancelStreaming();
  }
}
