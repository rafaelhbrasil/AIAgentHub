export type MessageRole = 'User' | 'Assistant' | 'System' | 'Tool' | 0 | 1 | 2 | 3;

export function isUserRole(role: MessageRole | string | number | null | undefined): boolean {
  if (role === 0 || role === '0') return true;
  if (typeof role === 'string') {
    return role.trim().toLowerCase() === 'user';
  }
  return false;
}

export interface ExecutionMetadata {
  providerId?: string;
  modelId?: string;
  tokensUsed?: number;
  durationMs?: number;
  additionalData?: Record<string, string>;
}

export interface MessageDto {
  id: string;
  conversationId: string;
  role: MessageRole | string | number;
  content: string;
  createdAtUtc: string;
  metadata?: ExecutionMetadata | null;
}

export interface ConversationDto {
  id: string;
  workspaceId: string;
  title: string;
  providerId: string;
  modelId?: string | null;
  effort?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  messageCount: number;
  fileChangeCount: number;
}

export interface ConversationDetailDto {
  id: string;
  workspaceId: string;
  title: string;
  providerId: string;
  modelId?: string | null;
  effort?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  messages: MessageDto[];
}
