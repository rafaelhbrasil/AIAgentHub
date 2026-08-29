import React, { useState, useEffect } from 'react';
import { apiFetch } from '../../services/apiClient';
import { ProviderDto, ModelInfo } from '../../types/provider';
import { sortProviders } from '../../utils/providerSort';
import { ProviderSkeletons } from '../common/Skeletons';
import { ProviderCard } from './ProviderCard';
import { useModal } from '../../context/ModalContext';
import { useToast } from '../../context/ToastContext';
import { ProviderModelsModal } from './ProviderModelsModal';
import { ProviderSettingsModal } from '../modals/ProviderSettingsModal';
import { InstallInstructionsModal } from './InstallInstructionsModal';
import { ExternalLinkDisclaimerModal } from './ExternalLinkDisclaimerModal';
import { ProviderRefreshModal } from './ProviderRefreshModal';

export const ProvidersView: React.FC = () => {
  const { showModal, hideModal } = useModal();
  const { showToast } = useToast();
  const [providers, setProviders] = useState<ProviderDto[]>([]);
  const [showHidden, setShowHidden] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const fetchProviders = async () => {
    setIsLoading(true);
    try {
      const res = await apiFetch<ProviderDto[]>('/api/v1/providers');
      if (res.ok && res.data) {
        setProviders(res.data);
      } else {
        showToast('Failed to load providers.', 'error');
      }
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchProviders();
  }, []);

  const handleOpenRefreshAllModal = () => {
    showModal(
      '🔄 Refreshing AI Providers',
      <ProviderRefreshModal
        onComplete={(updatedProviders) => {
          setProviders(updatedProviders);
          showToast('All providers refreshed successfully.', 'success');
        }}
        onClose={hideModal}
      />
    );
  };

  const handleOpenSettingsModal = (provider: ProviderDto) => {
    showModal(
      `⚙️ Provider Settings — ${provider.displayName}`,
      <ProviderSettingsModal
        provider={provider}
        onSaved={async (updated: ProviderDto) => {
          setProviders((prev) =>
            prev.map((p) => (p.id === provider.id ? { ...p, ...updated } : p))
          );
          hideModal();
          await fetchProviders();
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleOpenModelsModal = (provider: ProviderDto) => {
    showModal(
      `⚙️ ${provider.displayName} — Available Models`,
      <ProviderModelsModal
        provider={provider}
        initialModels={provider.supportedModels}
        onSaveSuccess={async (updatedModels: ModelInfo[]) => {
          setProviders((prev) =>
            prev.map((p) => (p.id === provider.id ? { ...p, supportedModels: updatedModels } : p))
          );
          hideModal();
          await fetchProviders();
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleOpenInstallModal = (provider: ProviderDto) => {
    showModal(
      `📥 Install ${provider.displayName}`,
      <InstallInstructionsModal
        provider={provider}
        onOpenDocUrl={handleOpenExternalLink}
        onClose={hideModal}
      />
    );
  };

  const handleOpenExternalLink = (url: string) => {
    showModal(
      '⚠️ External Site Disclaimer',
      <ExternalLinkDisclaimerModal
        url={url}
        onProceed={() => {
          hideModal();
          window.open(url, '_blank', 'noopener,noreferrer');
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleStatusUpdated = (updatedProvider: ProviderDto) => {
    setProviders((prev) => prev.map((p) => (p.id === updatedProvider.id ? updatedProvider : p)));
  };

  if (isLoading) {
    return <ProviderSkeletons />;
  }

  const displayedProviders = showHidden ? providers : providers.filter((p) => !p.isHidden);
  const sorted = sortProviders(displayedProviders);
  const hiddenCount = providers.filter((p) => p.isHidden).length;

  return (
    <div>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '20px',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <h2>AI Providers</h2>
          {hiddenCount > 0 && (
            <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.85rem', color: 'var(--text-muted)', cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={showHidden}
                onChange={(e) => setShowHidden(e.target.checked)}
                style={{ cursor: 'pointer' }}
              />
              <span>Show hidden ({hiddenCount})</span>
            </label>
          )}
        </div>
        <button
          type="button"
          className="btn btn-secondary"
          id="refreshProvBtn"
          onClick={handleOpenRefreshAllModal}
        >
          🔄 Refresh All Providers
        </button>
      </div>

      <div className="grid-cols-3" id="providersGrid">
        {sorted.map((p) => (
          <ProviderCard
            key={p.id}
            provider={p}
            onOpenModelsModal={handleOpenModelsModal}
            onOpenSettingsModal={handleOpenSettingsModal}
            onOpenInstallModal={handleOpenInstallModal}
            onOpenExternalLink={handleOpenExternalLink}
            onStatusUpdated={handleStatusUpdated}
          />
        ))}
      </div>

      <div className="last-updated">Updated: {new Date().toLocaleTimeString()}</div>
    </div>
  );
};
