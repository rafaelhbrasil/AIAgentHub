import React from 'react';
import { WorkspaceDto } from '../../../types/workspace';
import { ConversationDetailDto, ConversationStatus } from '../../../types/conversation';
import { ModelInfo } from '../../../types/provider';
import { StudioActionsDropdown } from './StudioActionsDropdown';

interface StudioHeaderProps {
  workspace: WorkspaceDto;
  activeConversation: ConversationDetailDto | null;
  models: ModelInfo[];
  showActionsMenu: boolean;
  isStreaming?: boolean;
  onBack: () => void;
  onModelChange: (modelId: string) => void;
  onToggleActionsMenu: () => void;
  onCloseActionsMenu: () => void;
  onNewConversation: () => void;
  onOpenDiffs: () => void;
  onDownloadZip: () => void;
  onSwitchProvider?: () => void;
  onEffortChange: (effort: string) => void;
  onDeleteConversation: (id: string, title: string) => void;
}

export const StudioHeader: React.FC<StudioHeaderProps> = ({
  workspace,
  activeConversation,
  models,
  showActionsMenu,
  isStreaming,
  onBack,
  onModelChange,
  onToggleActionsMenu,
  onCloseActionsMenu,
  onNewConversation,
  onOpenDiffs,
  onDownloadZip,
  onSwitchProvider,
  onEffortChange,
  onDeleteConversation,
}) => {
  return (
    <div className="studio-compact-header glass">
      <div className="studio-header-left">
        <button
          type="button"
          className="btn btn-secondary compact-btn"
          id="backToWsList"
          onClick={onBack}
          title="Back to Workspaces"
        >
          &larr; <span className="hide-on-mobile">Workspaces</span>
        </button>

        <div className="studio-crumbs">
          <span className="studio-crumb-ws" title={workspace.path}>
            📁 {workspace.name}
          </span>
          {activeConversation && (() => {
            const isSwitching =
              activeConversation.status === ConversationStatus.SwitchingProvider ||
              (activeConversation.status as any) === 1;

            return (
              <>
                <span className="studio-crumb-sep">/</span>
                <span
                  className="studio-conv-title"
                  title={`ID: ${activeConversation.id}\n${activeConversation.title}`}
                >
                  {activeConversation.title}
                </span>
                <button
                  type="button"
                  className={`badge badge-provider ${isSwitching ? 'badge-provider-switching' : ''}`}
                  disabled={isStreaming || isSwitching}
                  onClick={isStreaming ? undefined : onSwitchProvider}
                  title={
                    isStreaming
                      ? 'Cannot switch provider while command is running. Please wait for it to finish or abort it.'
                      : isSwitching
                      ? 'Provider migration in progress. Click to view status or abort.'
                      : `Active Provider: ${activeConversation.providerId}. Click to switch provider.`
                  }
                  style={{
                    cursor: onSwitchProvider && !isStreaming ? 'pointer' : 'default',
                    border: isSwitching ? '1px solid rgba(234, 179, 8, 0.4)' : 'none',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                    transition: 'all 0.2s',
                    opacity: isStreaming ? 0.75 : 1,
                    background: isSwitching ? 'rgba(234, 179, 8, 0.15)' : undefined,
                    color: isSwitching ? '#fde047' : undefined,
                  }}
                >
                  <span>{isSwitching ? '⏳ Migrating...' : `⚡ ${activeConversation.providerId}`}</span>
                  {onSwitchProvider && !isStreaming && (
                    <span style={{ fontSize: '0.75rem', opacity: 0.75 }}>
                      {isSwitching ? '⚠️' : '🔄'}
                    </span>
                  )}
                </button>
              </>
            );
          })()}
        </div>
      </div>

      <div className="studio-header-right">
        {activeConversation && (() => {
          const isSwitching =
            activeConversation.status === ConversationStatus.SwitchingProvider ||
            (activeConversation.status as any) === 1;

          return (
            <select
              id="convModelSelect"
              className="form-select compact-select"
              value={activeConversation.modelId || ''}
              onChange={(e) => onModelChange(e.target.value)}
              disabled={isSwitching}
              title={isSwitching ? 'Cannot change model while provider migration is in progress' : 'Active Model'}
            >
              <option value="">Default Model</option>
              {models
                .filter((m) => m.id && m.id.toLowerCase() !== 'default')
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.displayName || m.id}
                  </option>
                ))}
            </select>
          );
        })()}

        {/* Quick Actions Menu Trigger */}
        <div className="studio-actions-dropdown-wrap">
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            id="optionsMenuBtn"
            onClick={onToggleActionsMenu}
            title="Workspace & Conversation Options"
          >
            ⚙️ <span className="hide-on-mobile">Options</span>
          </button>

          <StudioActionsDropdown
            isOpen={showActionsMenu}
            onClose={onCloseActionsMenu}
            activeConversation={activeConversation}
            workspaceId={workspace.id}
            isStreaming={isStreaming}
            onNewConversation={onNewConversation}
            onOpenDiffs={onOpenDiffs}
            onDownloadZip={onDownloadZip}
            onSwitchProvider={onSwitchProvider}
            onEffortChange={onEffortChange}
            onDeleteConversation={onDeleteConversation}
          />
        </div>
      </div>
    </div>
  );
};
