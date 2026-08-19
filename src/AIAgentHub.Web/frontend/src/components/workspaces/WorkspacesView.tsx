import React, { useState, useEffect, useCallback } from 'react';
import { apiFetch } from '../../services/apiClient';
import { WorkspaceDto } from '../../types/workspace';
import { useModal } from '../../context/ModalContext';
import { useToast } from '../../context/ToastContext';
import { FolderExplorerModal } from '../modals/FolderExplorerModal';
import { WorkspaceStudioView } from './WorkspaceStudioView';

interface WorkspacesViewProps {
  initialWorkspaceId?: string | null;
  initialConversationId?: string | null;
  onNavigateToWorkspace?: (wsId: string, convId?: string | null) => void;
  onBackToWorkspaces?: () => void;
}

export const WorkspacesView: React.FC<WorkspacesViewProps> = ({
  initialWorkspaceId,
  initialConversationId,
  onNavigateToWorkspace,
  onBackToWorkspaces,
}) => {
  const { showModal, hideModal } = useModal();
  const { showToast } = useToast();
  const [workspaces, setWorkspaces] = useState<WorkspaceDto[]>([]);
  const [currentWorkspace, setCurrentWorkspace] = useState<WorkspaceDto | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const fetchWorkspaces = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await apiFetch<WorkspaceDto[]>('/api/v1/workspaces');
      if (res.ok && res.data) {
        setWorkspaces(res.data);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchWorkspaces();
  }, [fetchWorkspaces]);

  // Synchronize currentWorkspace cleanly when initialWorkspaceId changes
  useEffect(() => {
    if (initialWorkspaceId && workspaces.length > 0) {
      const match = workspaces.find((w) => w.id === initialWorkspaceId);
      if (match) {
        setCurrentWorkspace(match);
      }
    } else if (!initialWorkspaceId) {
      setCurrentWorkspace(null);
    }
  }, [initialWorkspaceId, workspaces]);

  const handleOpenStudio = (ws: WorkspaceDto) => {
    setCurrentWorkspace(ws);
    onNavigateToWorkspace?.(ws.id);
  };

  const handleBack = () => {
    setCurrentWorkspace(null);
    onBackToWorkspaces?.();
  };

  const handleShowCreateModal = () => {
    showModal(
      'Open or Create Workspace',
      <FolderExplorerModal
        onSuccess={(id: string) => {
          hideModal();
          fetchWorkspaces().then(() => {
            onNavigateToWorkspace?.(id);
          });
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleConfirmRemove = (ws: WorkspaceDto) => {
    showModal(
      'Remove Workspace',
      <div>
        <p>
          Are you sure you want to remove the workspace <strong>"{ws.name}"</strong>?
        </p>
        <div
          style={{
            background: 'rgba(99, 102, 241, 0.1)',
            border: '1px solid rgba(99, 102, 241, 0.3)',
            borderRadius: '6px',
            padding: '12px',
            marginTop: '14px',
            fontSize: '0.88rem',
            color: '#a5b4fc',
          }}
        >
          ℹ️ <strong>Note:</strong> This only removes the workspace from AI Agent Hub. Your local folder
          and project files at <code>{ws.path}</code> will <strong>NOT</strong> be deleted.
        </div>
      </div>,
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button type="button" className="btn btn-secondary" onClick={hideModal}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-danger"
          id="confirmDeleteWsBtn"
          onClick={async () => {
            const res = await apiFetch(`/api/v1/workspaces/${ws.id}`, { method: 'DELETE' });
            hideModal();
            if (res.ok || res.status === 204) {
              showToast(`Workspace "${ws.name}" removed from Agent Hub.`, 'success');
              if (currentWorkspace?.id === ws.id) {
                setCurrentWorkspace(null);
                onBackToWorkspaces?.();
              }
              fetchWorkspaces();
            } else {
              showToast('Failed to remove workspace.', 'error');
            }
          }}
        >
          Remove Workspace
        </button>
      </div>
    );
  };

  if (currentWorkspace) {
    return (
      <WorkspaceStudioView
        workspace={currentWorkspace}
        initialConversationId={initialConversationId}
        onConversationChanged={(convId) => {
          onNavigateToWorkspace?.(currentWorkspace.id, convId);
        }}
        onBack={handleBack}
        onRemoveWorkspace={handleConfirmRemove}
      />
    );
  }

  return (
    <div>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '20px',
        }}
      >
        <h2>Workspaces</h2>
        <button
          type="button"
          className="btn btn-primary"
          id="createWsBtn"
          onClick={handleShowCreateModal}
        >
          + Add Workspace
        </button>
      </div>

      {isLoading ? (
        <div style={{ color: 'var(--text-muted)', padding: '20px' }}>Loading workspaces...</div>
      ) : (
        <div className="grid-cols-3">
          {workspaces.map((w) => (
            <div key={w.id} className="card glass">
              <div className="card-title">
                <span>{w.name}</span>
                <span className="badge badge-provider">
                  {w.settings?.defaultProviderId || 'antigravity'}
                </span>
              </div>
              <div className="card-subtitle" style={{ wordBreak: 'break-all' }}>
                {w.path}
              </div>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginTop: '14px',
                }}
              >
                <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                  {w.conversationCount} conversations
                </span>
                <div style={{ display: 'flex', gap: '6px' }}>
                  <button
                    type="button"
                    className="btn btn-danger remove-ws-btn"
                    style={{ padding: '6px 10px', fontSize: '0.8rem' }}
                    onClick={() => handleConfirmRemove(w)}
                  >
                    🗑️
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary open-ws-btn"
                    onClick={() => handleOpenStudio(w)}
                  >
                    Open Studio
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
