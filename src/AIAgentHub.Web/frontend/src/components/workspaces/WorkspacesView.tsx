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
  const [showArchived, setShowArchived] = useState<boolean>(false);
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

  const handleToggleFavorite = async (ws: WorkspaceDto, isFavorite: boolean) => {
    const res = await apiFetch<WorkspaceDto>(`/api/v1/workspaces/${ws.id}/favorite`, {
      method: 'PUT',
      body: { isFavorite },
    });
    if (res.ok && res.data) {
      setWorkspaces((prev) => prev.map((w) => (w.id === ws.id ? res.data! : w)));
      showToast(isFavorite ? `"${ws.name}" added to favorites.` : `"${ws.name}" removed from favorites.`, 'info');
    }
  };

  const handleToggleArchive = async (ws: WorkspaceDto, isArchived: boolean) => {
    const res = await apiFetch<WorkspaceDto>(`/api/v1/workspaces/${ws.id}/archive`, {
      method: 'PUT',
      body: { isArchived },
    });
    if (res.ok && res.data) {
      setWorkspaces((prev) => prev.map((w) => (w.id === ws.id ? res.data! : w)));
      showToast(isArchived ? `"${ws.name}" archived.` : `"${ws.name}" restored from archive.`, 'info');
    }
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
            color: 'var(--text-main)',
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

  const handleConversationChanged = useCallback(
    (convId: string | null) => {
      if (currentWorkspace) {
        onNavigateToWorkspace?.(currentWorkspace.id, convId);
      }
    },
    [currentWorkspace, onNavigateToWorkspace]
  );

  if (currentWorkspace) {
    return (
      <WorkspaceStudioView
        workspace={currentWorkspace}
        initialConversationId={initialConversationId}
        onConversationChanged={handleConversationChanged}
        onBack={handleBack}
        onRemoveWorkspace={handleConfirmRemove}
      />
    );
  }

  const activeWorkspaces = workspaces
    .filter((w) => !w.isArchived)
    .sort((a, b) => {
      if (a.isFavorite && !b.isFavorite) return -1;
      if (!a.isFavorite && b.isFavorite) return 1;
      return a.name.localeCompare(b.name);
    });

  const archivedWorkspaces = workspaces.filter((w) => w.isArchived);

  const renderCard = (w: WorkspaceDto) => (
    <div key={w.id} className="card glass">
      <div className="card-title">
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <button
            type="button"
            className="btn-icon-plain"
            onClick={() => handleToggleFavorite(w, !w.isFavorite)}
            title={w.isFavorite ? 'Remove from favorites' : 'Mark as favorite'}
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              fontSize: '1rem',
              color: w.isFavorite ? '#f59e0b' : 'var(--text-muted)',
              opacity: w.isFavorite ? 1 : 0.5,
              transition: 'opacity 0.2s, color 0.2s',
            }}
          >
            {w.isFavorite ? '⭐' : '☆'}
          </button>
          <span>{w.name}</span>
        </div>
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
            className="btn btn-secondary"
            style={{ padding: '6px 10px', fontSize: '0.8rem' }}
            onClick={() => handleToggleArchive(w, !w.isArchived)}
            title={w.isArchived ? 'Restore from archive' : 'Archive workspace'}
          >
            {w.isArchived ? '📂' : '📦'}
          </button>
          <button
            type="button"
            className="btn btn-danger remove-ws-btn"
            style={{ padding: '6px 10px', fontSize: '0.8rem' }}
            onClick={() => handleConfirmRemove(w)}
            title="Remove workspace"
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
  );

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
      ) : activeWorkspaces.length === 0 && archivedWorkspaces.length === 0 ? (
        <div style={{ color: 'var(--text-muted)', padding: '20px', textAlign: 'center' }}>
          No workspaces yet. Click "+ Add Workspace" to get started.
        </div>
      ) : (
        <>
          <div className="grid-cols-3">
            {activeWorkspaces.map(renderCard)}
          </div>

          {archivedWorkspaces.length > 0 && (
            <div style={{ marginTop: '32px' }}>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  cursor: 'pointer',
                  fontSize: '0.95rem',
                  fontWeight: 600,
                  color: 'var(--text-muted)',
                  marginBottom: '12px',
                }}
                onClick={() => setShowArchived((prev) => !prev)}
              >
                <span>{showArchived ? '▼' : '▶'}</span>
                <span>Archived Workspaces ({archivedWorkspaces.length})</span>
              </div>
              {showArchived && (
                <div className="grid-cols-3" style={{ opacity: 0.85 }}>
                  {archivedWorkspaces.map(renderCard)}
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
};
