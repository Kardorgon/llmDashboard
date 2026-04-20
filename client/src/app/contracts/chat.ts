export type ChatRole = 'system' | 'user' | 'assistant';

export interface ChatMessage {
  role: ChatRole;
  content: string;
}

export interface DashboardKpi {
  id: string;
  label: string;
  value: number;
  unit: string;
  trendPercent: number;
}

export interface DashboardRegion {
  region: string;
  revenue: number;
  deals: number;
  churnPercent: number;
}

export interface DashboardSnapshot {
  title: string;
  generatedAtIso: string;
  kpis: DashboardKpi[];
  regions: DashboardRegion[];
}

export interface ChatRequest {
  dashboardSnapshotId: string;
  dashboardSnapshot: DashboardSnapshot;
  messages: ChatMessage[];
}

export type ChatChunkType = 'meta' | 'chunk' | 'done' | 'error';

export interface ChatStreamChunk {
  type: ChatChunkType;
  delta?: string;
  message?: string;
  provider?: string;
  model?: string;
}

export interface ChatUiMessage {
  id: string;
  role: Exclude<ChatRole, 'system'>;
  content: string;
  createdAtIso: string;
  isStreaming?: boolean;
  hasError?: boolean;
}
