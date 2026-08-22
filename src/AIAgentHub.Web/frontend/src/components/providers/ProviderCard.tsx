import React, { useState, useEffect } from 'react';
import { ProviderDto, ModelInfo, ProviderStatusDto } from '../../types/provider';
import { formatModelsSummary } from '../../utils/formatting';
import {
  isDiscontinuedStatus,
  isReadyStatus,
  isNotInstalledStatus,
  isUnauthenticatedStatus,
  isQuotaExceededStatus,
} from '../../utils/providerSort';
import { apiFetch } from '../../services/apiClient';
import { useToast } from '../../context/ToastContext';

interface ProviderCardProps {
  provider: ProviderDto;
  onOpenModelsModal: (provider: ProviderDto) => void;
  onOpenInstallModal: (provider: ProviderDto) => void;
  onOpenExternalLink: (url: string) => void;
  onStatusUpdated?: (updatedProvider: ProviderDto) => void;
}

export const ProviderCard: React.FC<ProviderCardProps> = ({
  provider,
  onOpenModelsModal,
  onOpenInstallModal,
  onOpenExternalLink,
  onStatusUpdated,
}) => {
  const { showToast } = useToast();
  const [currentProvider, setCurrentProvider] = useState<ProviderDto>(provider);
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false);

  useEffect(() => {
    setCurrentProvider(provider);
  }, [provider]);

  const isDiscontinued = isDiscontinuedStatus(currentProvider.status);
  const isReady = isReadyStatus(currentProvider.status);
  const isNotInstalled = isNotInstalledStatus(currentProvider.status);
  const isUnauthenticated = isUnauthenticatedStatus(currentProvider.status);
  const isQuotaExceeded = isQuotaExceededStatus(currentProvider.status);

  let statusText = 'Unknown';
  let statusClass = 'badge';

  if (isDiscontinued) {
    statusText = 'Discontinued';
    statusClass = 'badge badge-error';
  } else if (isReady) {
    statusText = 'Operational';
    statusClass = 'badge badge-ready';
  } else if (isNotInstalled) {
    statusText = 'Not Installed';
    statusClass = 'badge badge-notinstalled';
  } else if (isUnauthenticated) {
    statusText = 'Not Authenticated';
    statusClass = 'badge badge-warning';
  } else if (isQuotaExceeded) {
    statusText = 'Quota Exceeded';
    statusClass = 'badge badge-error';
  }

  const message = currentProvider.message || '';
  const messageBg =
    isDiscontinued || isQuotaExceeded
      ? 'rgba(239, 68, 68, 0.1)'
      : isReady
      ? 'rgba(34, 197, 94, 0.1)'
      : isNotInstalled
      ? 'rgba(239, 68, 68, 0.1)'
      : 'rgba(251, 191, 36, 0.1)';

  const messageColor =
    isDiscontinued || isQuotaExceeded
      ? '#ef4444'
      : isReady
      ? '#22c55e'
      : isNotInstalled
      ? '#ef4444'
      : '#fbbf24';

  const handleRefresh = async () => {
    setIsRefreshing(true);
    try {
      const [statusRes, modelsRes] = await Promise.all([
        apiFetch<ProviderStatusDto>(`/api/v1/providers/${currentProvider.id}/status?refresh=true`),
        apiFetch<ModelInfo[]>(`/api/v1/providers/${currentProvider.id}/models?refresh=true`),
      ]);

      const updated: ProviderDto = {
        ...currentProvider,
        status: statusRes.ok && statusRes.data ? statusRes.data.status : currentProvider.status,
        message: statusRes.ok && statusRes.data ? statusRes.data.message : currentProvider.message,
        quotaResetsAt: statusRes.ok && statusRes.data ? statusRes.data.quotaResetsAt : currentProvider.quotaResetsAt,
        documentationUrl:
          (statusRes.ok && statusRes.data?.documentationUrl) || currentProvider.documentationUrl,
        supportedModels: modelsRes.ok && modelsRes.data ? modelsRes.data : currentProvider.supportedModels,
      };

      setCurrentProvider(updated);
      onStatusUpdated?.(updated);
      showToast(`${currentProvider.displayName} refreshed.`, 'info');
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleAuthenticate = async () => {
    const res = await apiFetch<{ message?: string }>(`/api/v1/providers/${currentProvider.id}/authenticate`, {
      method: 'POST',
    });
    if (res.ok) {
      showToast(res.data?.message || 'Launched authentication.', 'success');
      setTimeout(handleRefresh, 3000);
    } else {
      showToast('Authentication failed.', 'error');
    }
  };

  return (
    <div className="card glass" id={`provider-card-${currentProvider.id}`}>
      <div className="card-title">
        <span>{currentProvider.displayName}</span>
        <span className={statusClass} id={`provider-status-${currentProvider.id}`}>
          {isRefreshing ? 'Checking...' : statusText}
        </span>
      </div>
      <div className="card-subtitle">{currentProvider.description}</div>

      <div style={{ margin: '12px 0', fontSize: '0.85rem' }}>
        <strong>Models:</strong>{' '}
        <button
          type="button"
          className="btn-link-inline"
          onClick={() => onOpenModelsModal(currentProvider)}
        >
          {formatModelsSummary(currentProvider.supportedModels)}
        </button>
      </div>

      {message && (
        <div
          style={{
            padding: '10px',
            borderRadius: '4px',
            fontSize: '0.85rem',
            marginBottom: '12px',
            background: messageBg,
            color: messageColor,
          }}
        >
          {message}
        </div>
      )}

      <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
        {isNotInstalled && (
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => onOpenInstallModal(currentProvider)}
          >
            📥 Install Instructions
          </button>
        )}
        {isUnauthenticated && (
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleAuthenticate}
          >
            🔑 Authenticate
          </button>
        )}
        <button
          type="button"
          className="btn btn-secondary"
          id={`refresh-btn-${currentProvider.id}`}
          onClick={handleRefresh}
          disabled={isRefreshing}
        >
          {isRefreshing ? (
            <>
              <span className="spinner-sm" style={{ marginRight: '6px' }} /> Checking...
            </>
          ) : (
            '🔄 Refresh'
          )}
        </button>
        {currentProvider.documentationUrl && (
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => onOpenExternalLink(currentProvider.documentationUrl!)}
          >
            🌐 Official Website
          </button>
        )}
      </div>
    </div>
  );
};
