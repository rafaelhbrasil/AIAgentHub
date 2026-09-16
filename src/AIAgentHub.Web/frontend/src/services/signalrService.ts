import * as signalR from '@microsoft/signalr';

export interface StreamChunkPayload {
  conversationId: string;
  chunk: string;
}

export interface TerminalOutputPayload {
  conversationId: string;
  chunk: string;
}

export interface TerminalMetaPayload {
  conversationId: string;
  thinkingTokens?: number | null;
  activeBackgroundTask?: string | null;
  currentTool?: string | null;
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
  public onTerminalOutput?: (payload: TerminalOutputPayload) => void;
  public onTerminalMeta?: (payload: TerminalMetaPayload) => void;
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

    this.connection.on('terminalOutput', (data: any) => {
      const conversationId = (data.conversationId || data.ConversationId || '').toString();
      const chunk = data.chunk || data.Chunk || '';
      this.onTerminalOutput?.({ conversationId, chunk });
    });

    this.connection.on('terminalMeta', (data: any) => {
      const conversationId = (data.conversationId || data.ConversationId || '').toString();
      this.onTerminalMeta?.({
        conversationId,
        thinkingTokens: data.thinkingTokens ?? data.ThinkingTokens,
        activeBackgroundTask: data.activeBackgroundTask ?? data.ActiveBackgroundTask,
        currentTool: data.currentTool ?? data.CurrentTool,
      });
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

  public async sendTerminalInput(conversationId: string, input: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('SendTerminalInput', conversationId, input);
    }
  }

  public async requestTerminalHistory(conversationId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('RequestTerminalHistory', conversationId);
    }
  }

  public async startTerminalSession(conversationId: string, workspacePath: string, cols = 120, rows = 30): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('StartTerminalSession', conversationId, workspacePath, cols, rows);
    }
  }

  public async restartTerminalSession(conversationId: string, workspacePath: string, cols = 120, rows = 30): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('RestartTerminalSession', conversationId, workspacePath, cols, rows);
    }
  }

  public async resizeTerminal(conversationId: string, cols: number, rows: number): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('ResizeTerminal', conversationId, cols, rows);
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
