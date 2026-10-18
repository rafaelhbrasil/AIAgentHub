import React from 'react';
import { useToast } from '../../context/ToastContext';

export const ToastContainer: React.FC = () => {
  const { toasts, removeToast } = useToast();

  if (toasts.length === 0) return null;

  return (
    <div id="toastContainer" className="toast-container">
      {toasts.map((toast) => (
        <button
          type="button"
          key={toast.id}
          className={`toast ${toast.type}`}
          aria-label="Dismiss notification"
          onClick={() => removeToast(toast.id)}
          style={{ cursor: 'pointer', textAlign: 'left', font: 'inherit' }}
        >
          <span>
            {toast.type === 'success'
              ? '✔️'
              : toast.type === 'error'
              ? '❌'
              : toast.type === 'warning'
              ? '⚠️'
              : 'ℹ️'}
          </span>
          <span>{toast.message}</span>
        </button>
      ))}
    </div>
  );
};
