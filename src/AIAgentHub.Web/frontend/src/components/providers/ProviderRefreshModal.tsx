import React, { useState, useEffect } from 'react';
import { ProviderDto, ProviderStatusDto } from '../../types/provider';
import {
  isDiscontinuedStatus,
  isReadyStatus,
  isUnauthenticatedStatus,
  isQuotaExceededStatus,
} from '../../utils/providerSort';

interface ProviderItemState {
  id: string;
  displayName: string;
  isCompleted: boolean;
  status?: ProviderStatusDto;
  message?: string;
}

interface ProviderRefreshModalProps {
  onComplete: (providers: ProviderDto[]) => void;
  onClose: () => void;
}

export const ProviderRefreshModal: React.FC<ProviderRefreshModalProps> = ({ onComplete, onClose }) => {
  const [items, setItems] = useState<ProviderItemState[]>([]);
  const [completedCount, setCompletedCount] = useState<number>(0);
  const [totalInstalled, setTotalInstalled] = useState<number>(0);
  const [percentage, setPercentage] = useState<number>(0);
  const [isDone, setIsDone] = useState<boolean>(false);
  const [statusText, setStatusText] = useState<string>('Detecting installed providers...');

  useEffect(() => {
    let isCancelled = false;
    const eventSource = new EventSource('/api/v1/providers/refresh-stream');

    eventSource.addEventListener('init', (e: MessageEvent) => {
      if (isCancelled) return;
      try {
        const data = JSON.parse(e.data);
        const count = data.totalInstalled || 0;
        setTotalInstalled(count);
        if (data.providers && data.providers.length > 0) {
          setItems(
            data.providers.map((p: { id: string; displayName: string }) => ({
              id: p.id,
              displayName: p.displayName,
              isCompleted: false,
            }))
          );
          setStatusText(`Checking ${count} provider${count > 1 ? 's' : ''} in parallel...`);
        } else {
          setStatusText('No installed providers detected.');
        }
      } catch (err) {
        console.error('Error parsing init event', err);
      }
    });

    eventSource.addEventListener('provider_completed', (e: MessageEvent) => {
      if (isCancelled) return;
      try {
        const data = JSON.parse(e.data);
        setCompletedCount(data.completedCount || 0);
        setPercentage(data.percentage || 0);

        setItems((prev) =>
          prev.map((item) =>
            item.id === data.provider?.id
              ? {
                  ...item,
                  isCompleted: true,
                  status: data.provider?.status,
                  message: data.provider?.message,
                }
              : item
          )
        );
      } catch (err) {
        console.error('Error parsing provider_completed event', err);
      }
    });

    eventSource.addEventListener('completed', (e: MessageEvent) => {
      if (isCancelled) return;
      try {
        const data = JSON.parse(e.data);
        setPercentage(100);
        setIsDone(true);
        setStatusText('Refresh completed successfully.');
        eventSource.close();
        if (data.providers) {
          onComplete(data.providers);
        }
      } catch (err) {
        console.error('Error parsing completed event', err);
        eventSource.close();
      }
    });

    eventSource.onerror = (err) => {
      if (isCancelled) return;
      console.warn('SSE stream encountered error or finished', err);
      eventSource.close();
      setIsDone(true);
    };

    return () => {
      isCancelled = true;
      eventSource.close();
    };
  }, [onComplete]);

  const renderStatusBadge = (item: ProviderItemState) => {
    if (!item.isCompleted) {
      return (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
          <span className="spinner-sm" /> Checking...
        </span>
      );
    }

    const status = item.status;
    if (isReadyStatus(status)) {
      return <span className="badge badge-ready">Operational ✅</span>;
    }
    if (isUnauthenticatedStatus(status)) {
      return <span className="badge badge-warning">Not authenticated ⚠️</span>;
    }
    if (isQuotaExceededStatus(status)) {
      return <span className="badge badge-error">Quota Exceeded ⏳</span>;
    }
    if (isDiscontinuedStatus(status)) {
      return <span className="badge badge-error">Discontinued ⏹️</span>;
    }
    return <span className="badge badge-error">Failed ❌</span>;
  };

  return (
    <div style={{ padding: '8px 0', minWidth: '440px' }} id="providerRefreshModalContainer">
      <div style={{ marginBottom: '16px' }}>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            marginBottom: '8px',
            fontSize: '0.9rem',
            color: 'var(--text-main)',
          }}
        >
          <span>{statusText}</span>
          <strong style={{ fontFamily: 'var(--font-mono)' }}>
            {totalInstalled > 0 ? `${completedCount} / ${totalInstalled} (${percentage}%)` : `${percentage}%`}
          </strong>
        </div>
        <div
          style={{
            width: '100%',
            height: '10px',
            backgroundColor: 'rgba(255, 255, 255, 0.1)',
            borderRadius: '6px',
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              width: `${percentage}%`,
              height: '100%',
              background: 'linear-gradient(90deg, var(--accent-primary, #6366f1), var(--accent-success, #10b981))',
              transition: 'width 0.3s ease',
            }}
          />
        </div>
      </div>

      <div
        style={{
          maxHeight: '260px',
          overflowY: 'auto',
          border: '1px solid var(--border-color)',
          borderRadius: '8px',
          padding: '10px 14px',
          marginBottom: '20px',
          background: 'rgba(0, 0, 0, 0.25)',
        }}
      >
        {items.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '16px', color: 'var(--text-muted)', fontSize: '0.9rem' }}>
            <span className="spinner-sm" style={{ marginRight: '8px' }} /> Detecting installed providers in system...
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {items.map((item) => (
              <div
                key={item.id}
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '6px 0',
                  borderBottom: '1px solid rgba(255, 255, 255, 0.05)',
                }}
              >
                <span style={{ fontWeight: 500, fontSize: '0.92rem' }}>{item.displayName}</span>
                {renderStatusBadge(item)}
              </div>
            ))}
          </div>
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button
          type="button"
          className="btn btn-primary"
          id="closeRefreshModalBtn"
          onClick={onClose}
          disabled={!isDone}
        >
          {isDone ? 'Close' : 'Please wait...'}
        </button>
      </div>
    </div>
  );
};
