import React from 'react';

interface StudioEmptyStateProps {
  onCreateConversation: () => void;
}

export const StudioEmptyState: React.FC<StudioEmptyStateProps> = ({ onCreateConversation }) => {
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        color: 'var(--text-muted)',
        padding: '40px',
        textAlign: 'center',
      }}
    >
      <div style={{ fontSize: '3rem', marginBottom: '12px', opacity: 0.5 }}>💬</div>
      <h3 style={{ marginBottom: '8px', color: 'var(--text-heading)' }}>No Active Conversation</h3>
      <p style={{ fontSize: '0.9rem', maxWidth: '400px', marginBottom: '16px' }}>
        Create a new conversation or select one from the sidebar to begin pair programming.
      </p>
      <button type="button" className="btn btn-primary" onClick={onCreateConversation}>
        + Start New Conversation
      </button>
    </div>
  );
};
