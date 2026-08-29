import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { WorkspaceDto } from '../../types/workspace';
import { ProviderDto } from '../../types/provider';
import { DashboardSkeletons } from '../common/Skeletons';
import { useModal } from '../../context/ModalContext';
import { useToast } from '../../context/ToastContext';
import { FolderExplorerModal } from '../modals/FolderExplorerModal';
import { isProviderOperational } from '../../utils/providerSort';

interface DashboardViewProps {
  onOpenWorkspace: (workspaceId: string) => void;
}

export const DashboardView: React.FC<DashboardViewProps> = ({ onOpenWorkspace }) => {
  const { showModal, hideModal } = useModal();
  const { showToast } = useToast();
  const [workspaces, setWorkspaces] = useState<WorkspaceDto[]>([]);
  const [providers, setProviders] = useState<ProviderDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const fetchDashboardData = async () => {
    setIsLoading(true);
    try {
      const [wsRes, provRes] = await Promise.all([
        apiFetch<WorkspaceDto[]>('/api/v1/workspaces'),
        apiFetch<ProviderDto[]>('/api/v1/providers'),
      ]);

      if (wsRes.ok && wsRes.data) setWorkspaces(wsRes.data);
      if (provRes.ok && provRes.data) setProviders(provRes.data);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const handleShowCreateModal = () => {
    showModal(
      'Open or Create Workspace',
      <FolderExplorerModal
        onSuccess={(id: string) => {
          hideModal();
          onOpenWorkspace(id);
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
              fetchDashboardData();
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

  if (isLoading) {
    return <DashboardSkeletons />;
  }

  const activeWorkspaces = workspaces
    .filter((w) => !w.isArchived)
    .sort((a, b) => {
      if (a.isFavorite && !b.isFavorite) return -1;
      if (!a.isFavorite && b.isFavorite) return 1;
      return a.name.localeCompare(b.name);
    });

  const operationalCount = providers.filter(isProviderOperational).length;

  return (
    <div>
      <div className="grid-cols-3">
        <div className="card glass">
          <div className="card-title">
            Managed Workspaces <span>📁</span>
          </div>
          <div className="card-subtitle">Active local projects</div>
          <div className="stat-val">{activeWorkspaces.length}</div>
        </div>
        <div className="card glass">
          <div className="card-title">
            Available Providers <span>⚡</span>
          </div>
          <div className="card-subtitle">Operational AI engines</div>
          <div className="stat-val">
            {operationalCount} / {providers.length}
          </div>
        </div>
        <div className="card glass">
          <div className="card-title">
            Security & Port <span>🔒</span>
          </div>
          <div className="card-subtitle">HTTPS Self-Signed TLS</div>
          <div className="stat-val" style={{ fontSize: '1.6rem', color: '#34d399' }}>
            Port 5432
          </div>
        </div>
      </div>

      <div className="card glass" style={{ marginBottom: '24px' }}>
        <div className="card-title responsive-flex-header">
          <span>Recent Workspaces</span>
          <button
            type="button"
            className="btn btn-primary"
            id="dashNewWsBtn"
            onClick={handleShowCreateModal}
          >
            + Open or Create Workspace
          </button>
        </div>
        <div style={{ marginTop: '16px' }}>
          {activeWorkspaces.length === 0 ? (
            <p className="card-subtitle">
              No workspaces opened yet. Click above to open a folder on the server.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
              {activeWorkspaces.map((w) => (
                <div key={w.id} className="workspace-item-row">
                  <div className="workspace-item-info">
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      {w.isFavorite && <span title="Favorite workspace">⭐</span>}
                      <strong>{w.name}</strong>
                    </div>
                    <div className="workspace-item-path" title={w.path}>{w.path}</div>
                  </div>
                  <div className="workspace-item-actions">
                    <button
                      type="button"
                      className="btn btn-secondary open-ws-btn"
                      onClick={() => onOpenWorkspace(w.id)}
                    >
                      Open &rarr;
                    </button>
                    <button
                      type="button"
                      className="btn btn-danger remove-ws-btn"
                      style={{ padding: '6px 10px', fontSize: '0.8rem' }}
                      onClick={() => handleConfirmRemove(w)}
                    >
                      🗑️ Remove
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="last-updated">Updated: {new Date().toLocaleTimeString()}</div>
    </div>
  );
};
