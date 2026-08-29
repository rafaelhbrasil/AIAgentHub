import React from 'react';
import { ConversationDto } from '../../types/conversation';

interface ConversationListProps {
  conversations: ConversationDto[];
  activeConversationId?: string | null;
  onSelectConversation: (id: string) => void;
  onDeleteConversation: (id: string, title: string) => void;
  onTogglePin?: (id: string, isPinned: boolean) => void;
}

export const ConversationList: React.FC<ConversationListProps> = ({
  conversations,
  activeConversationId,
  onSelectConversation,
  onDeleteConversation,
  onTogglePin,
}) => {
  if (conversations.length === 0) {
    return (
      <p style={{ padding: '8px', color: 'var(--text-muted)', fontSize: '0.85rem', textAlign: 'center' }}>
        No conversations yet
      </p>
    );
  }

  // Sort: pinned first (by lastUserInteractionAtUtc desc), then unpinned (by lastUserInteractionAtUtc desc)
  const sortByInteraction = (a: ConversationDto, b: ConversationDto) => {
    const timeA = new Date(a.lastUserInteractionAtUtc || a.updatedAtUtc || a.createdAtUtc).getTime();
    const timeB = new Date(b.lastUserInteractionAtUtc || b.updatedAtUtc || b.createdAtUtc).getTime();
    return timeB - timeA;
  };

  const pinned = conversations.filter((c) => c.isPinned).sort(sortByInteraction);
  const unpinned = conversations.filter((c) => !c.isPinned).sort(sortByInteraction);

  const renderItem = (c: ConversationDto) => {
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
            fontSize: '0.88rem',
          }}
          title={c.title}
        >
          {c.isPinned ? '📌 ' : '💬 '}{c.title}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '2px' }}>
          {onTogglePin && (
            <button
              type="button"
              className="pin-conv-btn"
              title={c.isPinned ? 'Unpin conversation' : 'Pin conversation to top'}
              style={{
                background: 'transparent',
                border: 'none',
                color: c.isPinned ? '#f59e0b' : 'var(--text-muted)',
                cursor: 'pointer',
                padding: '2px 4px',
                fontSize: '0.8rem',
                borderRadius: '4px',
                opacity: c.isPinned ? 1 : 0.5,
                transition: 'opacity 0.2s, color 0.2s',
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.opacity = '1';
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.opacity = c.isPinned ? '1' : '0.5';
              }}
              onClick={(e) => {
                e.stopPropagation();
                onTogglePin(c.id, !c.isPinned);
              }}
            >
              📌
            </button>
          )}
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
      </div>
    );
  };

  return (
    <div id="conversationsList" style={{ padding: '10px', overflowY: 'auto', maxHeight: '240px' }}>
      {pinned.length > 0 && (
        <div style={{ marginBottom: '8px' }}>
          <div style={{ fontSize: '0.72rem', textTransform: 'uppercase', letterSpacing: '0.5px', color: 'var(--text-muted)', padding: '2px 8px 4px 8px', fontWeight: 600 }}>
            Pinned
          </div>
          {pinned.map(renderItem)}
        </div>
      )}
      {unpinned.length > 0 && (
        <div>
          {pinned.length > 0 && (
            <div style={{ fontSize: '0.72rem', textTransform: 'uppercase', letterSpacing: '0.5px', color: 'var(--text-muted)', padding: '6px 8px 4px 8px', fontWeight: 600 }}>
              Recent
            </div>
          )}
          {unpinned.map(renderItem)}
        </div>
      )}
    </div>
  );
};
