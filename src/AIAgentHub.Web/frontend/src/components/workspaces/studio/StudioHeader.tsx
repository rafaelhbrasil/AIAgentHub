import { WorkspaceDto } from '../../../types/workspace';
import { ConversationDetailDto, ConversationStatus } from '../../../types/conversation';
import { ModelInfo } from '../../../types/provider';
import { GitStatusDto } from '../../../types/git';
import { StudioActionsDropdown } from './StudioActionsDropdown';
import { GitBranchIcon } from '../../common/Icons';

interface StudioHeaderProps {
  workspace: WorkspaceDto;
  activeConversation: ConversationDetailDto | null;
  models: ModelInfo[];
  showActionsMenu: boolean;
  isStreaming?: boolean;
  gitStatus?: GitStatusDto | null;
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
  onToggleGit?: () => void;
  studioMode?: 'chat' | 'git';
  onSwitchStudioMode?: (mode: 'chat' | 'git') => void;
}

export const StudioHeader: React.FC<StudioHeaderProps> = ({
  workspace,
  activeConversation,
  models,
  showActionsMenu,
  isStreaming,
  gitStatus,
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
  onToggleGit,
  studioMode = 'chat',
  onSwitchStudioMode,
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

          {gitStatus?.isRepository && onToggleGit && (
            <button
              type="button"
              className="badge badge-provider desktop-only"
              id="studioGitBadgeBtn"
              onClick={onToggleGit}
              title={`Git branch: ${gitStatus.currentBranch || 'detached'}. Click to toggle Git panel.`}
              style={{
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '5px',
                background: 'rgba(34, 197, 94, 0.12)',
                border: '1px solid rgba(34, 197, 94, 0.3)',
                color: '#4ade80',
                marginLeft: '6px',
              }}
            >
              <GitBranchIcon size={12} style={{ color: '#4ade80' }} />
              <span>{gitStatus.currentBranch || 'Git'}</span>
              {gitStatus.ahead > 0 && <span style={{ fontSize: '0.72rem' }}>↑{gitStatus.ahead}</span>}
              {gitStatus.behind > 0 && <span style={{ fontSize: '0.72rem' }}>↓{gitStatus.behind}</span>}
            </button>
          )}
        </div>

        {onSwitchStudioMode && (
          <div className="segmented-switch desktop-only" style={{ display: 'inline-flex', marginLeft: '12px' }} role="group" aria-label="Studio View Mode">
            <button
              type="button"
              className={`btn compact-btn ${studioMode === 'chat' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => onSwitchStudioMode('chat')}
              id="studioModeChatBtn"
              style={{
                borderTopRightRadius: 0,
                borderBottomRightRadius: 0,
                fontSize: '0.78rem',
                padding: '3px 10px',
              }}
            >
              💬 Chat
            </button>
            <button
              type="button"
              className={`btn compact-btn ${studioMode === 'git' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => onSwitchStudioMode('git')}
              id="studioModeGitBtn"
              style={{
                borderTopLeftRadius: 0,
                borderBottomLeftRadius: 0,
                fontSize: '0.78rem',
                padding: '3px 10px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '5px',
              }}
            >
              <GitBranchIcon size={12} style={{ color: studioMode === 'git' ? '#fff' : '#4ade80' }} />
              <span>Git Workbench</span>
            </button>
          </div>
        )}
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
