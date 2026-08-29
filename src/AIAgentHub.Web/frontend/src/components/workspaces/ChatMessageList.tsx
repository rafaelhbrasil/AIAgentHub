import React, { useEffect, useLayoutEffect, useRef, useCallback } from 'react';
import { MessageDto, isUserRole, ConversationStatus } from '../../types/conversation';
import { formatMessageContent, escapeHtml } from '../../utils/markdown';
import { formatTime } from '../../utils/formatting';

interface ChatMessageListProps {
  messages: MessageDto[];
  providerId: string;
  streamingContent?: string;
  isStreaming?: boolean;
  heartbeatMessages?: string[];
  status?: number;
}

export const ChatMessageList: React.FC<ChatMessageListProps> = ({
  messages,
  providerId,
  streamingContent,
  isStreaming,
  heartbeatMessages,
  status,
}) => {
  const listRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  const isSwitching = status === ConversationStatus.SwitchingProvider || status === 1;

  const scrollToBottom = useCallback((behavior: ScrollBehavior = 'instant') => {
    if (listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight;
    }
    bottomRef.current?.scrollIntoView({ behavior, block: 'end' });
  }, []);

  useLayoutEffect(() => {
    scrollToBottom('instant');
  }, [messages, streamingContent, isStreaming, heartbeatMessages, isSwitching, scrollToBottom]);

  useEffect(() => {
    scrollToBottom('instant');
    const rafId = requestAnimationFrame(() => {
      scrollToBottom('instant');
    });
    const timerId = setTimeout(() => {
      scrollToBottom('instant');
    }, 50);

    return () => {
      cancelAnimationFrame(rafId);
      clearTimeout(timerId);
    };
  }, [messages, streamingContent, isStreaming, heartbeatMessages, isSwitching, scrollToBottom]);

  return (
    <div className="chat-messages" id="messageList" ref={listRef}>
      {isSwitching && (
        <div
          className="switching-provider-banner"
          style={{
            background: 'rgba(99, 102, 241, 0.15)',
            border: '1px solid rgba(99, 102, 241, 0.4)',
            borderRadius: '8px',
            padding: '10px 14px',
            marginBottom: '12px',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            color: '#818cf8',
            fontSize: '0.88rem',
          }}
        >
          <span style={{ fontSize: '1.1rem' }}>🔄</span>
          <div>
            <strong>Switching Provider...</strong> Context is compiling and synchronizing with target provider session.
          </div>
        </div>
      )}

      {messages.map((m) => {
        const isUser = isUserRole(m.role);
        const originProv = m.originProviderId || m.metadata?.providerId || providerId;
        const originModel = m.originModelId || m.metadata?.modelId;
        const isDifferentProvider = !isUser && originProv.toLowerCase() !== providerId.toLowerCase();

        return (
          <div
            key={m.id}
            className={`message-item ${isUser ? 'message-user' : 'message-assistant'}`}
          >
            <div className="message-header">
              <span>
                {isUser ? (
                  '👤 You'
                ) : (
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                    <span>⚡ AI Assistant ({originProv})</span>
                    {originModel && (
                      <span
                        style={{
                          fontSize: '0.72rem',
                          background: 'rgba(255, 255, 255, 0.08)',
                          padding: '1px 5px',
                          borderRadius: '4px',
                          fontFamily: 'var(--font-mono)',
                        }}
                      >
                        {originModel}
                      </span>
                    )}
                    {isDifferentProvider && (
                      <span
                        style={{
                          fontSize: '0.68rem',
                          background: 'rgba(245, 158, 11, 0.15)',
                          color: '#f59e0b',
                          padding: '1px 5px',
                          borderRadius: '4px',
                          fontWeight: 600,
                        }}
                        title={`Executed via previous provider session: ${originProv}`}
                      >
                        prev session
                      </span>
                    )}
                  </span>
                )}
              </span>
              <span style={{ marginLeft: 'auto' }}>{formatTime(m.createdAtUtc)}</span>
            </div>
            <div
              className="message-body markdown-rendered"
              dangerouslySetInnerHTML={{ __html: formatMessageContent(m.content) }}
            />
          </div>
        );
      })}

      {isStreaming && (
        <div className="message-item message-assistant streaming-active" id="activeStreamingMsg">
          <div className="message-header">
            <span>⚡ AI Assistant ({providerId})</span>
            <span style={{ marginLeft: 'auto' }}>Streaming...</span>
          </div>
          <div
            className="message-body markdown-rendered"
            id="streamingBody"
            dangerouslySetInnerHTML={{
              __html: streamingContent
                ? formatMessageContent(streamingContent)
                : heartbeatMessages && heartbeatMessages.length > 0
                  ? heartbeatMessages.map((m) => `<div style="margin-bottom: 4px;"><em>⏳ ${escapeHtml(m)}</em></div>`).join('')
                  : '<em>Thinking...</em>',
            }}
          />
        </div>
      )}

      <div id="streamingAnchor" ref={bottomRef}></div>
    </div>
  );
};
