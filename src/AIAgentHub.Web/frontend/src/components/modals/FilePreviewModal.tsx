import React from 'react';

interface FilePreviewModalProps {
  relativePath: string;
  renderedHtml: string;
  onClose: () => void;
}

export const FilePreviewModal: React.FC<FilePreviewModalProps> = ({ renderedHtml, onClose }) => {
  return (
    <div>
      <div
        style={{ maxHeight: '60vh', overflow: 'auto', marginBottom: '16px' }}
        dangerouslySetInnerHTML={{ __html: renderedHtml }}
      />
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button type="button" className="btn btn-secondary" onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  );
};
