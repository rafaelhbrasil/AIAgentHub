import * as signalR from '@microsoft/signalr';

export interface StreamChunkPayload {
  conversationId: string;
  chunk: string;
}

export interface ConversationEventPayload {
  conversationId: string;
  eventName: string;
  data?: any;
}

export interface NotificationPayload {
  title: string;
  message: string;
  level: 'info' | 'error' | 'success' | 'warning';
}

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private currentConversationId: string | null = null;

  public onStreamChunk?: (payload: StreamChunkPayload) => void;
  public onConversationEvent?: (payload: ConversationEventPayload) => void;
  public onPermissionRequested?: (req: any) => void;
  public onDiffCreated?: (diff: any) => void;
  public onNotification?: (notification: NotificationPayload) => void;
  public onReconnected?: () => void;

  public start(): void {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/agent')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.connection.onreconnected(() => {
      if (this.currentConversationId) {
        this.connection?.invoke('JoinConversation', this.currentConversationId).catch(() => {});
      }
      this.onReconnected?.();
    });

    this.connection.on('streamChunk', (data: any) => {
      const conversationId = (data.conversationId || data.ConversationId || '').toString();
      const chunk = data.chunk || data.Chunk || '';
      this.onStreamChunk?.({ conversationId, chunk });
    });

    this.connection.on('conversationEvent', (data: any) => {
      const conversationId = (data.conversationId || data.ConversationId || '').toString();
      const eventName = data.eventName || data.EventName || '';
      this.onConversationEvent?.({ conversationId, eventName, data });
    });

    this.connection.on('permissionRequested', (req: any) => {
      this.onPermissionRequested?.(req);
    });

    this.connection.on('diffCreated', (diff: any) => {
      this.onDiffCreated?.(diff);
    });

    this.connection.on('notification', (n: any) => {
      this.onNotification?.({
        title: n.title || 'Notification',
        message: n.message || '',
        level: n.level === 'error' ? 'error' : 'info',
      });
    });

    this.connection
      .start()
      .then(() => {
        if (this.currentConversationId) {
          this.joinConversation(this.currentConversationId);
        }
      })
      .catch((err) => console.log('SignalR connection error:', err));
  }

  public joinConversation(conversationId: string): void {
    this.currentConversationId = conversationId;
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      this.connection.invoke('JoinConversation', conversationId).catch(() => {});
    }
  }

  public stop(): void {
    if (this.connection) {
      this.connection.stop().catch(() => {});
      this.connection = null;
    }
  }
}

export const signalRService = new SignalRService();
