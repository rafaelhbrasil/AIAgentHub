export enum MessageRole {
  User = 'User',
  Assistant = 'Assistant',
  System = 'System',
  Tool = 'Tool',
}

export enum ConversationStatus {
  Active = 0,
  SwitchingProvider = 1,
  Locked = 2,
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
  sequenceIndex?: number;
  originProviderId?: string | null;
  originModelId?: string | null;
}

export interface ConversationProviderSessionDto {
  id: string;
  conversationId: string;
  providerId: string;
  providerSessionId?: string | null;
  lastSharedMessageId?: string | null;
  lastSharedSequenceIndex: number;
  createdAtUtc: string;
  lastActiveAtUtc: string;
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
  status?: ConversationStatus | number;
  isPinned?: boolean;
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
  status?: ConversationStatus | number;
  isPinned?: boolean;
  messages: MessageDto[];
  sessions?: ConversationProviderSessionDto[];
}

export interface SwitchProviderRequest {
  targetProviderId: string;
  targetModelId?: string | null;
  historyScope?: string;
  includeFileChanges?: boolean;
}

export interface SwitchProviderResult {
  conversationId: string;
  activeProviderId: string;
  activeModelId?: string | null;
  migratedMessageCount: number;
  targetSessionId?: string | null;
}
