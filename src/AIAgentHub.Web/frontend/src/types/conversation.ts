export enum MessageRole {
  User = 'User',
  Assistant = 'Assistant',
  System = 'System',
  Tool = 'Tool',
}

export const isUserRole = (role?: MessageRole | string | null): boolean => role === MessageRole.User;

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
  role: MessageRole | string;
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
  lastUserInteractionAtUtc?: string;
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
  lastUserInteractionAtUtc?: string;
  messages: MessageDto[];
}
