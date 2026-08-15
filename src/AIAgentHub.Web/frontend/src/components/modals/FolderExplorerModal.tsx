import React, { useState, useEffect, useRef } from 'react';
import { apiFetch } from '../../services/apiClient';
import { DriveDto, DirectoryBrowserResult } from '../../types/workspace';
import { useToast } from '../../context/ToastContext';

interface FolderExplorerModalProps {
  onSuccess: (workspaceId: string) => void;
  onCancel: () => void;
}

export const FolderExplorerModal: React.FC<FolderExplorerModalProps> = ({ onSuccess, onCancel }) => {
  const { showToast } = useToast();
  const [drives, setDrives] = useState<DriveDto[]>([]);
  const [currentPath, setCurrentPath] = useState<string>('');
  const [wsName, setWsName] = useState<string>('');
  const [defaultProvider, setDefaultProvider] = useState<string>('antigravity');
  const [browserData, setBrowserData] = useState<DirectoryBrowserResult | null>(null);
  const [isLoadingFolders, setIsLoadingFolders] = useState<boolean>(false);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [selectedFolder, setSelectedFolder] = useState<string>('');
  const nativePickerRef = useRef<HTMLInputElement>(null);

  const updateSuggestedName = (fullPath: string) => {
    if (!fullPath) return;
    const clean = fullPath.replace(/[/\\]+$/, '');
    const parts = clean.split(/[/\\]/);
    if (parts.length > 0) {
      setWsName(parts[parts.length - 1] || 'Workspace');
    }
  };

  const loadDirectory = async (path: string) => {
    setCurrentPath(path);
    setSelectedFolder('');
    updateSuggestedName(path);
    setIsLoadingFolders(true);

    try {
      const url = path ? `/api/v1/filesystem/browse?path=${encodeURIComponent(path)}` : '/api/v1/filesystem/browse';
      const res = await apiFetch<DirectoryBrowserResult>(url);
      if (res.ok && res.data) {
        setBrowserData(res.data);
        setCurrentPath(res.data.currentPath);
        updateSuggestedName(res.data.currentPath);
      } else {
        setBrowserData(null);
        showToast('Failed to access directory: ' + (res.error || 'Permission denied'), 'error');
      }
    } finally {
      setIsLoadingFolders(false);
    }
  };

  useEffect(() => {
    const init = async () => {
      const [drivesRes, browseRes] = await Promise.all([
        apiFetch<DriveDto[]>('/api/v1/filesystem/drives'),
        apiFetch<DirectoryBrowserResult>('/api/v1/filesystem/browse'),
      ]);

      if (drivesRes.ok && drivesRes.data) {
        setDrives(drivesRes.data);
      }

      if (browseRes.ok && browseRes.data) {
        setBrowserData(browseRes.data);
        setCurrentPath(browseRes.data.currentPath);
        updateSuggestedName(browseRes.data.currentPath);
      } else if (drivesRes.ok && drivesRes.data && drivesRes.data.length > 0) {
        loadDirectory(drivesRes.data[0].path);
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
            <button
              type="button"
              className="btn btn-secondary"
              style={{ padding: '3px 8px', fontSize: '0.8rem' }}
              onClick={() => loadDirectory(currentPath)}
              title="Refresh folder"
            >
              🔄
            </button>
          </div>

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
                    className={`folder-tile ${selectedFolder === d.fullPath ? 'selected' : ''}`}
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
        <select
          className="form-select"
          value={defaultProvider}
          onChange={(e) => setDefaultProvider(e.target.value)}
        >
          <option value="antigravity">Antigravity CLI — Google DeepMind</option>
          <option value="gemini">Gemini CLI</option>
          <option value="codex">OpenAI Codex CLI</option>
          <option value="claude">Claude Code</option>
          <option value="opencode">OpenCode</option>
        </select>
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
          disabled={isSubmitting || !currentPath.trim()}
        >
          {isSubmitting ? 'Opening Workspace...' : 'Open Workspace'}
        </button>
      </div>
    </div>
  );
};
