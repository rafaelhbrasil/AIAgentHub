import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { WorkspaceDto } from '../../types/workspace';
import { ProviderDto, ProviderStatus } from '../../types/provider';
import { useToast } from '../../context/ToastContext';
import { Spinner } from '../common/Spinner';

interface CloneWorkspaceModalProps {
  onSuccess: (workspaceId: string) => void;
  onCancel: () => void;
}

function trimTrailingSlashes(str: string): string {
  let end = str.length;
  while (end > 0 && (str[end - 1] === '/' || str[end - 1] === '\\')) {
    end--;
  }
  return str.slice(0, end);
}

export const extractRepoName = (url: string): string => {
  const trimmed = trimTrailingSlashes(url.trim());
  if (!trimmed) return '';
  const withoutGit = trimmed.toLowerCase().endsWith('.git') ? trimmed.slice(0, -4) : trimmed;
  const clean = trimTrailingSlashes(withoutGit);
  const lastDelim = Math.max(clean.lastIndexOf('/'), clean.lastIndexOf('\\'), clean.lastIndexOf(':'));
  return lastDelim >= 0 ? clean.slice(lastDelim + 1) : clean;
};

export const CloneWorkspaceModal: React.FC<CloneWorkspaceModalProps> = ({ onSuccess, onCancel }) => {
  const { showToast } = useToast();
  const [cloneUrl, setCloneUrl] = useState<string>('');
  const [targetDirectory, setTargetDirectory] = useState<string>('');
  const [workspaceName, setWorkspaceName] = useState<string>('');
  const [defaultProvider, setDefaultProvider] = useState<string>('');
  const [availableProviders, setAvailableProviders] = useState<ProviderDto[]>([]);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  useEffect(() => {
    const fetchProviders = async () => {
      try {
        const res = await apiFetch<ProviderDto[]>('/api/v1/providers');
        if (res.ok && res.data) {
          setAvailableProviders(res.data);
          const active = res.data.find((p) => p.isInstalled && p.status === ProviderStatus.Ready);
          if (active) {
            setDefaultProvider(active.id);
          }
        }
      } catch {
        // Ignore error
      }
    };
    fetchProviders();
  }, []);

  const handleUrlChange = (url: string) => {
    setCloneUrl(url);
    if (!workspaceName || workspaceName === extractRepoName(cloneUrl)) {
      const suggested = extractRepoName(url);
      if (suggested) {
        setWorkspaceName(suggested);
      }
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cloneUrl.trim()) {
      showToast('Please enter a Git repository URL.', 'warning');
      return;
    }
    if (!targetDirectory.trim()) {
      showToast('Please enter a target directory path.', 'warning');
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await apiFetch<WorkspaceDto>('/api/v1/workspaces/clone', {
        method: 'POST',
        body: {
          cloneUrl: cloneUrl.trim(),
          targetDirectory: targetDirectory.trim(),
          workspaceName: workspaceName.trim() || undefined,
          defaultProviderId: defaultProvider || undefined,
        },
      });

      if (res.ok && res.data) {
        showToast(`Workspace cloned successfully: ${res.data.name}`, 'success');
        onSuccess(res.data.id);
      } else {
        const errMessage = (res.data as any)?.message || res.error || 'Failed to clone repository.';
        showToast(errMessage, 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error during git clone.', 'error');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} style={{ minWidth: '400px', maxWidth: '600px' }}>
      <p style={{ color: 'var(--text-muted)', fontSize: '0.88rem', marginBottom: '16px' }}>
        Clone a remote Git repository into a local folder and register it as an active workspace.
      </p>

      <div className="form-group" style={{ marginBottom: '14px' }}>
        <label className="form-label" htmlFor="gitCloneUrl">
          Git Repository URL <span style={{ color: 'var(--accent-danger)' }}>*</span>
        </label>
        <input
          type="text"
          id="gitCloneUrl"
          className="form-control"
          placeholder="https://github.com/org/repo.git or git@github.com:org/repo.git"
          value={cloneUrl}
          onChange={(e) => handleUrlChange(e.target.value)}
          disabled={isSubmitting}
          required
          autoFocus
        />
      </div>

      <div className="form-group" style={{ marginBottom: '14px' }}>
        <label className="form-label" htmlFor="gitTargetDir">
          Target Directory <span style={{ color: 'var(--accent-danger)' }}>*</span>
        </label>
        <input
          type="text"
          id="gitTargetDir"
          className="form-control"
          placeholder="e.g. D:\Projects\MyRepo or /home/user/projects/my-repo"
          value={targetDirectory}
          onChange={(e) => setTargetDirectory(e.target.value)}
          disabled={isSubmitting}
          required
        />
        <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginTop: '4px' }}>
          Git will clone into this folder (or create it if it does not exist).
        </div>
      </div>

      <div className="form-group" style={{ marginBottom: '14px' }}>
        <label className="form-label" htmlFor="gitWsName">Workspace Name (Optional)</label>
        <input
          type="text"
          id="gitWsName"
          className="form-control"
          placeholder="Defaults to repository name"
          value={workspaceName}
          onChange={(e) => setWorkspaceName(e.target.value)}
          disabled={isSubmitting}
        />
      </div>

      {availableProviders.length > 0 && (
        <div className="form-group" style={{ marginBottom: '20px' }}>
          <label className="form-label" htmlFor="gitDefaultProvider">Default AI Provider</label>
          <select
            id="gitDefaultProvider"
            className="form-select"
            value={defaultProvider}
            onChange={(e) => setDefaultProvider(e.target.value)}
            disabled={isSubmitting}
          >
            <option value="">No preference</option>
            {availableProviders.map((p) => (
              <option key={p.id} value={p.id}>
                {p.displayName || p.id}
              </option>
            ))}
          </select>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '20px' }}>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onCancel}
          disabled={isSubmitting}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="btn btn-primary"
          id="submitCloneWsBtn"
          disabled={isSubmitting || !cloneUrl || !targetDirectory}
          style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
        >
          {isSubmitting && <Spinner size={16} isSpinning={true} />}
          <span>{isSubmitting ? 'Cloning repository...' : 'Clone & Open'}</span>
        </button>
      </div>
    </form>
  );
};
