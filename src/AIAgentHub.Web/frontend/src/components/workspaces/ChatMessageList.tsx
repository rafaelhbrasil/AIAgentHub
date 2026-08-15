import React, { useEffect, useRef } from 'react';
import { MessageDto, isUserRole } from '../../types/conversation';
import { formatMessageContent } from '../../utils/markdown';
import { formatTime } from '../../utils/formatting';

interface ChatMessageListProps {
  messages: MessageDto[];
  providerId: string;
  streamingContent?: string;
  isStreaming?: boolean;
}

export const ChatMessageList: React.FC<ChatMessageListProps> = ({
  messages,
  providerId,
  streamingContent,
  isStreaming,
}) => {
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight;
    }
  }, [messages, streamingContent]);

  return (
    <div className="chat-messages" id="messageList" ref={listRef}>
      {messages.map((m) => {
        const isUser = isUserRole(m.role);
        const prov = m.metadata?.providerId || providerId;

        return (
          <div
            key={m.id}
            className={`message-item ${isUser ? 'message-user' : 'message-assistant'}`}
          >
            <div className="message-header">
              <span>{isUser ? '👤 You' : `⚡ AI Assistant (${prov})`}</span>
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
              __html: streamingContent ? formatMessageContent(streamingContent) : '<em>Thinking...</em>',
            }}
          />
        </div>
      )}

      <div id="streamingAnchor"></div>
    </div>
  );
};
