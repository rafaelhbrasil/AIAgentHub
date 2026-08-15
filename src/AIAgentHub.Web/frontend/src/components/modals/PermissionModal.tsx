import React from 'react';
import { apiFetch } from '../../services/apiClient';
import { PermissionRequestDto } from '../../types/settings';
import { useToast } from '../../context/ToastContext';

interface PermissionModalProps {
  request: PermissionRequestDto;
  onClose: () => void;
}

export const PermissionModal: React.FC<PermissionModalProps> = ({ request, onClose }) => {
  const { showToast } = useToast();

  const handleDecision = async (approve: boolean) => {
    await apiFetch(`/api/v1/permissions/${request.id}/decide`, {
      method: 'POST',
      body: { approve },
    });
    onClose();
    showToast(approve ? 'Permission approved.' : 'Permission denied.', approve ? 'success' : 'info');
  };

  return (
    <div>
      <div className="card glass" style={{ marginBottom: '16px' }}>
        <p style={{ marginBottom: '6px' }}>
          <strong>Provider:</strong> {request.providerId}
        </p>
        <p style={{ marginBottom: '6px' }}>
          <strong>Action Type:</strong> {request.type}
        </p>
        <p style={{ marginBottom: '6px' }}>
          <strong>Target:</strong> <code>{request.target}</code>
        </p>
        <p style={{ margin: 0 }}>
          <strong>Reason:</strong> {request.reason}
        </p>
      </div>
      <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)', marginBottom: '16px' }}>
        Please explicitly approve or deny this operation.
      </p>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button type="button" className="btn btn-danger" onClick={() => handleDecision(false)}>
          Deny
        </button>
        <button type="button" className="btn btn-success" onClick={() => handleDecision(true)}>
          Approve & Continue
        </button>
      </div>
    </div>
  );
};
