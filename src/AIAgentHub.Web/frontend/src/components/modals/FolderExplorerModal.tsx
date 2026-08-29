import React, { useState, useEffect, useRef } from 'react';
import { apiFetch } from '../../services/apiClient';
import { DriveDto, DirectoryBrowserResult, ForbiddenPathsResponse } from '../../types/workspace';
import { ProviderDto } from '../../types/provider';
import { useToast } from '../../context/ToastContext';
import { isPathForbiddenForBrowsing, isPathForbiddenForWorkspace } from '../../utils/pathValidation';
import { isProviderOperational } from '../../utils/providerSort';
import { Spinner } from '../common/Spinner';

interface FolderExplorerModalProps {
  onSuccess: (workspaceId: string) => void;
  onCancel: () => void;
}

export const FolderExplorerModal: React.FC<FolderExplorerModalProps> = ({ onSuccess, onCancel }) => {
  const { showToast } = useToast();
  const [drives, setDrives] = useState<DriveDto[]>([]);
  const [forbiddenPaths, setForbiddenPaths] = useState<string[]>([]);
  const [availableProviders, setAvailableProviders] = useState<ProviderDto[]>([]);
  const [currentPath, setCurrentPath] = useState<string>('');
  const [wsName, setWsName] = useState<string>('');
  const [defaultProvider, setDefaultProvider] = useState<string>('');
  const [browserData, setBrowserData] = useState<DirectoryBrowserResult | null>(null);
  const [isLoadingFolders, setIsLoadingFolders] = useState<boolean>(false);
  const [isLoadingProviders, setIsLoadingProviders] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [isCreatingFolder, setIsCreatingFolder] = useState<boolean>(false);
  const [newFolderName, setNewFolderName] = useState<string>('');
  const nativePickerRef = useRef<HTMLInputElement>(null);

  const handleCreateFolder = async () => {
    const trimmed = newFolderName.trim();
    if (!trimmed || !currentPath) return;

    const separator = currentPath.includes('\\') ? '\\' : '/';
    const targetPath = `${currentPath.replace(/[/\\]+$/, '')}${separator}${trimmed}`;

    try {
      const res = await apiFetch<{ path: string }>('/api/v1/filesystem/mkdir', {
        method: 'POST',
        body: { path: targetPath },
      });

      if (res.ok && res.data) {
        showToast(`Created folder "${trimmed}".`, 'success');
        setNewFolderName('');
        setIsCreatingFolder(false);
        await loadDirectory(res.data.path);
      } else {
        showToast(res.error || (res.data as any)?.message || 'Failed to create folder.', 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error creating folder.', 'error');
    }
  };

  const updateSuggestedName = (fullPath: string) => {
    if (!fullPath) return;
    const clean = fullPath.replace(/[/\\]+$/, '');
    const parts = clean.split(/[/\\]/);
    if (parts.length > 0) {
      setWsName(parts[parts.length - 1] || 'Workspace');
    }
  };

  const loadDirectory = async (path: string, activeForbiddenList: string[] = forbiddenPaths) => {
    if (path && isPathForbiddenForBrowsing(path, activeForbiddenList)) {
      showToast(`The directory '${path}' is a protected system folder and cannot be opened.`, 'error');
      return;
    }

    setIsLoadingFolders(true);
    try {
      const res = await apiFetch<DirectoryBrowserResult>(
        `/api/v1/filesystem/browse?path=${encodeURIComponent(path)}`
      );
      if (res.ok && res.data) {
        setBrowserData(res.data);
        setCurrentPath(res.data.currentPath);
        updateSuggestedName(res.data.currentPath);
      } else {
        showToast(res.error || 'Failed to browse directory.', 'error');
      }
    } catch {
      showToast('Network error while browsing directory.', 'error');
    } finally {
      setIsLoadingFolders(false);
    }
  };

  useEffect(() => {
    const init = async () => {
      setIsLoadingProviders(true);
      const [drivesRes, browseRes, forbiddenRes, provRes] = await Promise.all([
        apiFetch<DriveDto[]>('/api/v1/filesystem/drives'),
        apiFetch<DirectoryBrowserResult>('/api/v1/filesystem/browse'),
        apiFetch<ForbiddenPathsResponse>('/api/v1/filesystem/forbidden-paths'),
        apiFetch<ProviderDto[]>('/api/v1/providers'),
      ]);

      let loadedForbidden: string[] = [];
      if (forbiddenRes.ok && forbiddenRes.data) {
        loadedForbidden = forbiddenRes.data.forbiddenPaths;
        setForbiddenPaths(loadedForbidden);
      }

      if (provRes.ok && provRes.data) {
        const operational = provRes.data.filter(isProviderOperational);
        setAvailableProviders(operational);
        if (operational.length > 0) {
          setDefaultProvider(operational[0].id);
        } else {
          setDefaultProvider('');
        }
      }
      setIsLoadingProviders(false);

      if (drivesRes.ok && drivesRes.data) {
        setDrives(drivesRes.data);
      }

      if (browseRes.ok && browseRes.data) {
        setBrowserData(browseRes.data);
        setCurrentPath(browseRes.data.currentPath);
        updateSuggestedName(browseRes.data.currentPath);
      } else if (drivesRes.ok && drivesRes.data && drivesRes.data.length > 0) {
        loadDirectory(drivesRes.data[0].path, loadedForbidden);
      }
    };
    init();
  }, []);

  const handleCreateWorkspace = async () => {
    const path = currentPath.trim();
    const name = wsName.trim();
    if (!path) {
      showToast('Path is required.', 'error');
      return;
    }

    if (isPathForbiddenForWorkspace(path, forbiddenPaths)) {
      showToast(`The directory '${path}' cannot be used as a workspace (root drives and protected system folders are restricted).`, 'error');
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await apiFetch<{ id: string }>('/api/v1/workspaces', {
        method: 'POST',
        body: { name, path, defaultProviderId: defaultProvider },
      });

      if (res.ok && res.data) {
        showToast('Workspace created successfully.', 'success');
        onSuccess(res.data.id);
      } else {
        showToast(res.error || (res.data as any)?.message || 'Failed to create workspace.', 'error');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const clean = currentPath.replace(/[/\\]+$/, '');
  const pathParts = clean.split(/[/\\]/);

  return (
    <div>
      <div className="form-group">
        <label className="form-label">Workspace Root Directory</label>
        <div style={{ display: 'flex', gap: '8px' }}>
          <input
            type="text"
            className="form-input"
            value={currentPath}
            onChange={(e) => setCurrentPath(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                loadDirectory(currentPath);
              }
            }}
          />
          <button
            type="button"
            className="btn btn-secondary"
            title="Choose local folder via browser picker"
            onClick={() => nativePickerRef.current?.click()}
          >
            📂 Local Picker...
          </button>
          <input
            type="file"
            ref={nativePickerRef}
            // @ts-expect-error webkitdirectory is standard for folder pickers
            webkitdirectory=""
            directory=""
            style={{ display: 'none' }}
            onChange={(e) => {
              if (e.target.files && e.target.files.length > 0) {
                const file = e.target.files[0];
                const relPath = file.webkitRelativePath || '';
                const topDir = relPath.split('/')[0];
                if (topDir) {
                  setWsName(topDir);
                  showToast(`Selected folder '${topDir}'. Verify full path in input.`, 'info');
                }
              }
            }}
          />
        </div>
      </div>

      {/* Windows-style Explorer Dialog */}
      <div className="explorer-dialog">
        {/* Left Quick Access Sidebar */}
        <div className="explorer-sidebar">
          <div className="explorer-section-title">Drives & Devices</div>
          {drives.map((d) => (
            <div
              key={d.name}
              className={`explorer-pin ${currentPath.toLowerCase().startsWith(d.path.toLowerCase()) ? 'active' : ''}`}
              onClick={() => loadDirectory(d.path)}
              title={`${d.name} (${d.driveType})`}
            >
              💾 <span>{d.name}</span>
            </div>
          ))}

          <div className="explorer-section-title" style={{ marginTop: '12px' }}>
            Quick Links
          </div>
          <div
            className="explorer-pin"
            onClick={() => {
              loadDirectory('');
            }}
          >
            👤 <span>Home Profile</span>
          </div>
        </div>

        {/* Main Folder Explorer Area */}
        <div className="explorer-main">
          <div className="explorer-nav-bar">
            <button
              type="button"
              className="btn btn-secondary"
              style={{ padding: '3px 8px', fontSize: '0.8rem' }}
              onClick={() => {
                if (browserData?.parentPath) {
                  loadDirectory(browserData.parentPath);
                }
              }}
              disabled={!browserData?.parentPath}
              title="Up one folder level"
            >
              ⬆️ Up
            </button>

            {/* Breadcrumb Path Bar */}
            <div className="explorer-breadcrumbs">
              {pathParts.map((part, idx) => {
                const subPath = pathParts.slice(0, idx + 1).join('\\') + (idx === 0 ? '\\' : '');
                return (
                  <React.Fragment key={idx}>
                    <button
                      type="button"
                      className="crumb-btn"
                      onClick={() => loadDirectory(subPath)}
                    >
                      {part || '\\'}
                    </button>
                    <span className="crumb-sep">&gt;</span>
                  </React.Fragment>
                );
              })}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                style={{ fontSize: '0.75rem', padding: '2px 8px' }}
                onClick={() => setIsCreatingFolder((prev) => !prev)}
                title="Create a new subfolder in this directory"
              >
                ➕ New Folder
              </button>
              <button
                type="button"
                className="btn-refresh-icon"
                onClick={() => loadDirectory(currentPath)}
                disabled={isLoadingFolders}
                title="Refresh folder"
              >
                <Spinner size={15} isSpinning={isLoadingFolders} />
              </button>
            </div>
          </div>

          {isCreatingFolder && (
            <div
              style={{
                display: 'flex',
                gap: '8px',
                padding: '8px',
                background: 'var(--bg-glass)',
                borderBottom: '1px solid var(--border-color)',
                alignItems: 'center',
              }}
            >
              <input
                type="text"
                className="form-input compact-input"
                style={{ flex: 1, padding: '4px 8px', fontSize: '0.85rem' }}
                placeholder="Folder name..."
                value={newFolderName}
                onChange={(e) => setNewFolderName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') handleCreateFolder();
                  if (e.key === 'Escape') setIsCreatingFolder(false);
                }}
                autoFocus
              />
              <button
                type="button"
                className="btn btn-primary compact-btn"
                style={{ padding: '4px 10px', fontSize: '0.8rem' }}
                onClick={handleCreateFolder}
                disabled={!newFolderName.trim()}
              >
                Create
              </button>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                style={{ padding: '4px 8px', fontSize: '0.8rem' }}
                onClick={() => {
                  setIsCreatingFolder(false);
                  setNewFolderName('');
                }}
              >
                ✕
              </button>
            </div>
          )}

          <div className="explorer-folder-list">
            {isLoadingFolders ? (
              <div style={{ color: 'var(--text-muted)', padding: '12px' }}>Loading folders...</div>
            ) : !browserData || !browserData.entries || browserData.entries.filter((e) => e.isDirectory).length === 0 ? (
              <div style={{ color: 'var(--text-muted)', padding: '12px', gridColumn: '1 / -1' }}>
                No subdirectories in this folder.
              </div>
            ) : (
              browserData.entries
                .filter((e) => e.isDirectory)
                .map((d) => (
                  <div
                    key={d.fullPath}
                    className={`folder-tile ${currentPath.toLowerCase() === d.fullPath.toLowerCase() ? 'selected' : ''}`}
                    onClick={() => loadDirectory(d.fullPath)}
                    title={`Click to open: ${d.name}`}
                  >
                    📁 <span>{d.name}</span>
                  </div>
                ))
            )}
          </div>
        </div>
      </div>

      <div className="form-group" style={{ marginTop: '14px' }}>
        <label className="form-label">Workspace Display Name</label>
        <input
          type="text"
          className="form-input"
          value={wsName}
          onChange={(e) => setWsName(e.target.value)}
          placeholder="Suggested automatically from folder name"
        />
      </div>

      <div className="form-group">
        <label className="form-label">Default AI Assistant Provider</label>
        {isLoadingProviders ? (
          <div style={{ color: 'var(--text-muted)', fontSize: '0.88rem', padding: '6px 0' }}>
            Loading available providers...
          </div>
        ) : availableProviders.length === 0 ? (
          <div
            style={{
              padding: '10px 14px',
              background: 'rgba(239, 68, 68, 0.1)',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              borderRadius: '6px',
              color: '#f87171',
              fontSize: '0.85rem',
              lineHeight: 1.5,
            }}
          >
            ⚠️ No operational AI providers are currently available. Please install and authenticate a provider in the{' '}
            <a
              href="/providers"
              onClick={(e) => {
                e.preventDefault();
                onCancel();
                window.history.pushState({}, '', '/providers');
                window.dispatchEvent(new PopStateEvent('popstate'));
              }}
              style={{ color: '#60a5fa', textDecoration: 'underline', fontWeight: 600, cursor: 'pointer' }}
            >
              AI Providers
            </a>{' '}
            page to configure one.
          </div>
        ) : (
          <select
            className="form-select"
            value={defaultProvider}
            onChange={(e) => setDefaultProvider(e.target.value)}
          >
            {availableProviders.map((p) => (
              <option key={p.id} value={p.id}>
                {p.displayName}
              </option>
            ))}
          </select>
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-primary"
          id="confirmCreateWsBtn"
          onClick={handleCreateWorkspace}
          disabled={isSubmitting || !currentPath.trim() || availableProviders.length === 0 || !defaultProvider}
        >
          {isSubmitting ? 'Opening Workspace...' : 'Open Workspace'}
        </button>
      </div>
    </div>
  );
};
