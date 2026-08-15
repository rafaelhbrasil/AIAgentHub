import React from 'react';
import { ConversationDto } from '../../types/conversation';

interface ConversationListProps {
  conversations: ConversationDto[];
  activeConversationId?: string | null;
  onSelectConversation: (id: string) => void;
  onDeleteConversation: (id: string, title: string) => void;
}

export const ConversationList: React.FC<ConversationListProps> = ({
  conversations,
  activeConversationId,
  onSelectConversation,
  onDeleteConversation,
}) => {
  if (conversations.length === 0) {
    return (
      <p style={{ padding: '8px', color: 'var(--text-muted)', fontSize: '0.85rem', textAlign: 'center' }}>
        No conversations yet
      </p>
    );
  }

  return (
    <div id="conversationsList" style={{ padding: '10px', overflowY: 'auto', maxHeight: '220px' }}>
      {conversations.map((c) => {
        const isActive = activeConversationId === c.id;
        return (
          <div
            key={c.id}
            className={`tree-item ${isActive ? 'active' : ''}`}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '6px',
              padding: '6px 8px',
              borderRadius: '6px',
              marginBottom: '2px',
            }}
          >
            <div
              className="select-conv-btn"
              onClick={() => onSelectConversation(c.id)}
              style={{
                flex: 1,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
                cursor: 'pointer',
              }}
              title={c.title}
            >
              💬 {c.title}
            </div>
            <button
              type="button"
              className="delete-conv-btn"
              title="Delete Conversation"
              style={{
                background: 'transparent',
                border: 'none',
                color: 'var(--text-muted)',
                cursor: 'pointer',
                padding: '2px 4px',
                fontSize: '0.8rem',
                borderRadius: '4px',
                opacity: 0.6,
                transition: 'opacity 0.2s, color 0.2s, background 0.2s',
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.opacity = '1';
                e.currentTarget.style.color = '#ef4444';
                e.currentTarget.style.background = 'rgba(239, 68, 68, 0.1)';
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.opacity = '0.6';
                e.currentTarget.style.color = 'var(--text-muted)';
                e.currentTarget.style.background = 'transparent';
              }}
              onClick={(e) => {
                e.stopPropagation();
                onDeleteConversation(c.id, c.title);
              }}
            >
              🗑️
            </button>
          </div>
        );
      })}
    </div>
  );
};
