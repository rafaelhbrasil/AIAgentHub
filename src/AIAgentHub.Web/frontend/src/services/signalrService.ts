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

const infiniteRetryPolicy: signalR.IRetryPolicy = {
  nextRetryDelayInMilliseconds: (retryContext: signalR.RetryContext) => {
    // Retry indefinitely with capped backoff up to 5000ms
    return Math.min(1000 * Math.pow(1.5, retryContext.previousRetryCount), 5000);
  },
};

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private currentConversationId: string | null = null;
  private retryTimeoutId: any = null;

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
      .withAutomaticReconnect(infiniteRetryPolicy)
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

    const connectWithRetry = () => {
      if (!this.connection) return;

      this.connection
        .start()
        .then(() => {
          if (this.currentConversationId) {
            this.joinConversation(this.currentConversationId);
          }
        })
        .catch((err) => {
          if (!this.connection) return;
          console.warn('SignalR connection failed, retrying in 5s...', err);
          this.retryTimeoutId = setTimeout(connectWithRetry, 5000);
        });
    };

    connectWithRetry();
  }

  public joinConversation(conversationId: string): void {
    this.currentConversationId = conversationId;
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      this.connection.invoke('JoinConversation', conversationId).catch(() => {});
    }
  }

  public stop(): void {
    if (this.retryTimeoutId) {
      clearTimeout(this.retryTimeoutId);
      this.retryTimeoutId = null;
    }
    if (this.connection) {
      this.connection.stop().catch(() => {});
      this.connection = null;
    }
  }
}

export const signalRService = new SignalRService();
