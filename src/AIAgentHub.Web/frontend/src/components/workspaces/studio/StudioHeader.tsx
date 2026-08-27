import React from 'react';
import { WorkspaceDto } from '../../../types/workspace';
import { ConversationDetailDto } from '../../../types/conversation';
import { ModelInfo } from '../../../types/provider';
import { StudioActionsDropdown } from './StudioActionsDropdown';

interface StudioHeaderProps {
  workspace: WorkspaceDto;
  activeConversation: ConversationDetailDto | null;
  models: ModelInfo[];
  showActionsMenu: boolean;
  onBack: () => void;
  onModelChange: (modelId: string) => void;
  onToggleActionsMenu: () => void;
  onCloseActionsMenu: () => void;
  onNewConversation: () => void;
  onOpenDiffs: () => void;
  onDownloadZip: () => void;
  onEffortChange: (effort: string) => void;
  onDeleteConversation: (id: string, title: string) => void;
}

export const StudioHeader: React.FC<StudioHeaderProps> = ({
  workspace,
  activeConversation,
  models,
  showActionsMenu,
  onBack,
  onModelChange,
  onToggleActionsMenu,
  onCloseActionsMenu,
  onNewConversation,
  onOpenDiffs,
  onDownloadZip,
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

        <div className="studio-title-block">
          <span className="studio-ws-badge" title={workspace.path}>
            📁 {workspace.name}
          </span>
          {activeConversation && (
            <>
              <span className="studio-crumb-sep">/</span>
              <span
                className="studio-conv-title"
                title={`ID: ${activeConversation.id}\n${activeConversation.title}`}
              >
                {activeConversation.title}
              </span>
              <span className="badge badge-provider">{activeConversation.providerId}</span>
            </>
          )}
        </div>
      </div>

      <div className="studio-header-right">
        {activeConversation && (
          <select
            id="convModelSelect"
            className="form-select compact-select"
            value={activeConversation.modelId || ''}
            onChange={(e) => onModelChange(e.target.value)}
            title="Active Model"
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
        )}

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
            onNewConversation={onNewConversation}
            onOpenDiffs={onOpenDiffs}
            onDownloadZip={onDownloadZip}
            onEffortChange={onEffortChange}
            onDeleteConversation={onDeleteConversation}
          />
        </div>
      </div>
    </div>
  );
};
