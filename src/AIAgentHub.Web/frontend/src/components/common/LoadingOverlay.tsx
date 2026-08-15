import React from 'react';

interface LoadingOverlayProps {
  isVisible: boolean;
  text?: string;
}

export const LoadingOverlay: React.FC<LoadingOverlayProps> = ({ isVisible, text = 'Loading...' }) => {
  if (!isVisible) return null;

  return (
    <div id="loadingOverlay" className="loading-overlay">
      <div className="loading-spinner"></div>
      <div className="loading-text">{text}</div>
    </div>
  );
};
