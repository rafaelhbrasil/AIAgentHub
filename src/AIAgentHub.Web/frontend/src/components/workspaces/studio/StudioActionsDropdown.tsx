import React from 'react';
import { ConversationDetailDto } from '../../../types/conversation';
import { useToast } from '../../../context/ToastContext';

interface StudioActionsDropdownProps {
  isOpen: boolean;
  onClose: () => void;
  activeConversation: ConversationDetailDto | null;
  workspaceId: string;
  isStreaming?: boolean;
  onNewConversation: () => void;
  onOpenDiffs: () => void;
  onDownloadZip: () => void;
  onSwitchProvider?: () => void;
  onEffortChange: (effort: string) => void;
  onDeleteConversation: (id: string, title: string) => void;
}

export const StudioActionsDropdown: React.FC<StudioActionsDropdownProps> = ({
  isOpen,
  onClose,
  activeConversation,
  workspaceId,
  isStreaming,
  onNewConversation,
  onOpenDiffs,
  onDownloadZip,
  onSwitchProvider,
  onEffortChange,
  onDeleteConversation,
}) => {
  const { showToast } = useToast();

  if (!isOpen) return null;

  return (
    <>
      <div className="dropdown-backdrop" onClick={onClose}></div>
      <div className="studio-actions-dropdown glass">
        <button
          type="button"
          className="dropdown-item"
          id="newConvBtn"
          onClick={() => {
            onClose();
            onNewConversation();
          }}
        >
          ➕ New Conversation
        </button>

        {activeConversation && onSwitchProvider && (
          <button
            type="button"
            className="dropdown-item"
            id="switchProviderDropdownBtn"
            disabled={isStreaming}
            title={isStreaming ? 'Cannot switch provider while command is running' : undefined}
            onClick={() => {
              if (isStreaming) return;
              onClose();
              onSwitchProvider();
            }}
          >
            🔄 Switch AI Provider...
          </button>
        )}

        <button
          type="button"
          className="dropdown-item"
          id="viewDiffsBtn"
          onClick={() => {
            onClose();
            onOpenDiffs();
          }}
          disabled={!activeConversation}
        >
          📝 Changed Files
        </button>

        <button
          type="button"
          className="dropdown-item"
          id="downloadZipDropdownBtn"
          onClick={() => {
            onClose();
            onDownloadZip();
          }}
        >
          📦 Download Project ZIP
        </button>

        {activeConversation && (
          <>
            <button
              type="button"
              className="dropdown-item"
              id="copyConvLinkBtn"
              onClick={() => {
                onClose();
                const url = `${window.location.origin}/workspaces/${workspaceId}/conversations/${activeConversation.id}`;
                navigator.clipboard.writeText(url);
                showToast('Conversation link copied to clipboard!', 'success');
              }}
            >
              🔗 Copy Conversation Link
            </button>

            <button
              type="button"
              className="dropdown-item"
              id="copyConvIdBtn"
              onClick={() => {
                onClose();
                navigator.clipboard.writeText(activeConversation.id);
                showToast('Conversation ID copied to clipboard!', 'success');
              }}
            >
              📋 Copy Conversation ID
            </button>
          </>
        )}

        {activeConversation && (
          <div className="dropdown-item-group">
            <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>
              Reasoning Effort:
            </label>
            <select
              id="convEffortSelect"
              className="form-select compact-select"
              value={activeConversation.effort || ''}
              onChange={(e) => {
                onEffortChange(e.target.value);
                onClose();
              }}
            >
              <option value="">Default Effort</option>
              <option value="low">Low Effort</option>
              <option value="medium">Medium Effort</option>
              <option value="high">High Effort</option>
              <option value="max">Max Effort</option>
            </select>
          </div>
        )}

        <div style={{ borderTop: '1px solid var(--border-color)', margin: '4px 0' }}></div>

        {activeConversation && (
          <button
            type="button"
            className="dropdown-item text-danger"
            id="deleteCurrentConvBtn"
            onClick={() => {
              onClose();
              onDeleteConversation(activeConversation.id, activeConversation.title);
            }}
          >
            🗑️ Delete Current Conversation
          </button>
        )}
      </div>
    </>
  );
};
