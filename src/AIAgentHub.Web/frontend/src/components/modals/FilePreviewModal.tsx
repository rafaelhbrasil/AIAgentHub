import React, { useState } from 'react';
import { FilePreviewDto } from '../../types/diff';
import { useToast } from '../../context/ToastContext';
import { renderMarkdown } from '../../utils/markdown';

interface FilePreviewModalProps {
  relativePath: string;
  preview: FilePreviewDto;
  onClose: () => void;
}

export const FilePreviewModal: React.FC<FilePreviewModalProps> = ({
  relativePath,
  preview,
  onClose,
}) => {
  const { showToast } = useToast();
  const [activeTab, setActiveTab] = useState<'preview' | 'raw'>('preview');
  const [copied, setCopied] = useState<boolean>(false);

  // Raw file is available for text-based files (markdown, source code, text, json, yaml, etc.)
  const canShowRaw = !preview.isBinary && typeof preview.rawText === 'string' && preview.rawText.length > 0;

  const handleCopyRaw = async () => {
    if (!preview.rawText) return;
    try {
      await navigator.clipboard.writeText(preview.rawText);
      setCopied(true);
      showToast('Raw content copied to clipboard!', 'success');
      setTimeout(() => setCopied(false), 2000);
    } catch {
      showToast('Failed to copy to clipboard.', 'error');
    }
  };

  const rawLines = canShowRaw && preview.rawText ? preview.rawText.split(/\r\n|\r|\n/) : [];

  const formatFileSize = (bytes?: number) => {
    if (bytes === undefined || bytes === null || bytes === 0) return '';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const isMarkdown = preview.contentType === 'text/markdown' ||
    relativePath.toLowerCase().endsWith('.md') ||
    relativePath.toLowerCase().endsWith('.markdown');

  const renderedPreviewHtml = isMarkdown && preview.rawText
    ? renderMarkdown(preview.rawText)
    : preview.renderedHtml;

  return (
    <div className="file-preview-modal-body">
      {/* Top Header / Tab Bar */}
      {canShowRaw && (
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: '12px',
            borderBottom: '1px solid var(--border-color)',
            paddingBottom: '8px',
          }}
        >
          {/* Tabs */}
          <div style={{ display: 'flex', gap: '6px' }}>
            <button
              type="button"
              className={`btn compact-btn ${activeTab === 'preview' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setActiveTab('preview')}
              style={{ fontSize: '0.8rem', padding: '4px 12px' }}
            >
              👁️ Preview
            </button>
            <button
              type="button"
              className={`btn compact-btn ${activeTab === 'raw' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setActiveTab('raw')}
              style={{ fontSize: '0.8rem', padding: '4px 12px' }}
            >
              📄 Raw File
            </button>
          </div>

          {/* Quick Copy Button */}
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            onClick={handleCopyRaw}
            style={{ fontSize: '0.78rem', padding: '3px 10px' }}
            title="Copy raw file content to clipboard"
          >
            {copied ? '✅ Copied!' : '📋 Copy Raw'}
          </button>
        </div>
      )}

      {/* Main View Area */}
      <div
        className="preview-content-container"
        style={{
          maxHeight: '62vh',
          minHeight: '200px',
          overflow: 'auto',
          background: '#090d16',
          padding: activeTab === 'raw' ? '0' : '16px',
          borderRadius: '8px',
          border: '1px solid var(--border-color)',
          marginBottom: '16px',
        }}
      >
        {activeTab === 'preview' || !canShowRaw ? (
          <div
            className="rendered-preview-content markdown-rendered"
            dangerouslySetInnerHTML={{ __html: renderedPreviewHtml }}
          />
        ) : (
          <div
            className="raw-file-view"
            style={{
              fontFamily: 'var(--font-mono)',
              fontSize: '0.84rem',
              lineHeight: '1.5',
              padding: '12px 0',
              color: '#e2e8f0',
              overflowX: 'auto',
            }}
          >
            {rawLines.map((line, idx) => (
              <div
                key={idx}
                style={{
                  display: 'flex',
                  padding: '1px 12px',
                  whiteSpace: 'pre',
                }}
                className="raw-line-row"
              >
                <span
                  style={{
                    width: '42px',
                    color: 'var(--text-muted)',
                    textAlign: 'right',
                    paddingRight: '14px',
                    userSelect: 'none',
                    flexShrink: 0,
                    opacity: 0.6,
                  }}
                >
                  {idx + 1}
                </span>
                <span style={{ flex: 1, whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
                  {line || ' '}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Footer Info & Close */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '10px',
        }}
      >
        <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>
          <strong style={{ color: 'var(--text-heading)', marginRight: '6px' }}>{relativePath}</strong>
          <span>({preview.contentType || 'file'})</span>
          {canShowRaw && rawLines.length > 0 && (
            <span> &bull; {rawLines.length} {rawLines.length === 1 ? 'line' : 'lines'}</span>
          )}
          {preview.sizeBytes !== undefined && preview.sizeBytes > 0 && (
            <span> &bull; {formatFileSize(preview.sizeBytes)}</span>
          )}
        </div>

        <button type="button" className="btn btn-secondary" onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  );
};
