import React from 'react';
import { FileChangeDto } from '../../../types/diff';
import { ChangeHunk, formatChangeType, isCreatedDiff, isDeletedDiff, isModifiedDiff } from './diffViewerUtils';

interface DiffControlsBarProps {
  activeDiff: FileChangeDto;
  changes: FileChangeDto[];
  currentFileIndex: number;
  isFullscreen: boolean;
  isEditing: boolean;
  changeHunks: ChangeHunk[];
  activeHunkIndex: number;
  isWordWrap: boolean;
  viewMode: 'sideBySide' | 'unified';
  sideBySideMobileTab: 'modified' | 'original' | 'split';
  onPrevFile: () => void;
  onNextFile: () => void;
  onPrevChange: () => void;
  onNextChange: () => void;
  onToggleWordWrap: () => void;
  onSetViewMode: (mode: 'sideBySide' | 'unified') => void;
  onSetFullscreen: (fullscreen: boolean) => void;
  onSetSideBySideMobileTab: (tab: 'modified' | 'original' | 'split') => void;
}

export const DiffControlsBar: React.FC<DiffControlsBarProps> = ({
  activeDiff,
  changes,
  currentFileIndex,
  isFullscreen,
  isEditing,
  changeHunks,
  activeHunkIndex,
  isWordWrap,
  viewMode,
  sideBySideMobileTab,
  onPrevFile,
  onNextFile,
  onPrevChange,
  onNextChange,
  onToggleWordWrap,
  onSetViewMode,
  onSetFullscreen,
  onSetSideBySideMobileTab,
}) => {
  const isCreated = isCreatedDiff(activeDiff);
  const isDeleted = isDeletedDiff(activeDiff);
  const isModified = isModifiedDiff(activeDiff);

  if (isFullscreen) {
    return (
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '8px',
          padding: '6px 12px',
          background: '#080c14',
          borderRadius: '6px',
          border: '1px solid var(--border-color)',
          flexShrink: 0,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', overflow: 'hidden' }}>
          {changes.length > 1 && (
            <div style={{ display: 'flex', gap: '4px', flexShrink: 0 }}>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                disabled={currentFileIndex <= 0}
                onClick={onPrevFile}
                title="Previous File (Alt+Left)"
                style={{ padding: '2px 6px', fontSize: '0.74rem' }}
              >
                ◀
              </button>
              <button
                type="button"
                className="btn btn-secondary compact-btn"
                disabled={currentFileIndex >= changes.length - 1}
                onClick={onNextFile}
                title="Next File (Alt+Right)"
                style={{ padding: '2px 6px', fontSize: '0.74rem' }}
              >
                ▶
              </button>
            </div>
          )}
          <strong
            style={{
              color: 'var(--text-heading)',
              fontSize: '0.84rem',
              whiteSpace: 'nowrap',
              textOverflow: 'ellipsis',
              overflow: 'hidden',
            }}
          >
            {activeDiff.relativePath}
          </strong>
        </div>

        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexShrink: 0 }}>
          {changeHunks.length > 0 && !isEditing && (
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '2px',
                background: 'rgba(255, 255, 255, 0.05)',
                borderRadius: '6px',
                padding: '2px 4px',
                border: '1px solid var(--border-color)',
              }}
            >
              <button
                type="button"
                className="btn compact-btn btn-secondary"
                style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                onClick={onPrevChange}
                title="Previous Change (Shift+F7 or Alt+Up)"
              >
                ▲ Prev
              </button>
              <span
                style={{
                  fontSize: '0.74rem',
                  color: 'var(--text-muted)',
                  fontFamily: 'var(--font-mono)',
                  minWidth: '60px',
                  textAlign: 'center',
                }}
              >
                {activeHunkIndex >= 0
                  ? `${activeHunkIndex + 1} / ${changeHunks.length}`
                  : `${changeHunks.length} diffs`}
              </span>
              <button
                type="button"
                className="btn compact-btn btn-secondary"
                style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                onClick={onNextChange}
                title="Next Change (F7 or Alt+Down)"
              >
                ▼ Next
              </button>
            </div>
          )}
          <button
            type="button"
            className="btn compact-btn btn-primary"
            style={{ padding: '4px 14px', fontSize: '0.74rem', flexShrink: 0 }}
            onClick={() => onSetFullscreen(false)}
            title="Close Full Screen (Escape)"
          >
            ✕ Close
          </button>
        </div>
      </div>
    );
  }

  return (
    <>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '8px',
          flexWrap: 'wrap',
          gap: '8px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '0.85rem', flexWrap: 'wrap' }}>
          <strong style={{ color: 'var(--text-heading)' }}>{activeDiff.relativePath}</strong>
          <span
            style={{
              fontSize: '0.72rem',
              padding: '1px 6px',
              borderRadius: '4px',
              background: isCreated
                ? 'rgba(74, 222, 128, 0.15)'
                : isDeleted
                ? 'rgba(248, 113, 113, 0.15)'
                : 'rgba(99, 102, 241, 0.15)',
              color: isCreated ? '#4ade80' : isDeleted ? '#f87171' : '#818cf8',
              fontWeight: 600,
            }}
          >
            {formatChangeType(activeDiff.changeType)}
          </span>
          {activeDiff.additionsCount !== undefined && activeDiff.additionsCount > 0 && (
            <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.78rem' }}>
              +{activeDiff.additionsCount}
            </span>
          )}
          {activeDiff.deletionsCount !== undefined && activeDiff.deletionsCount > 0 && (
            <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.78rem' }}>
              -{activeDiff.deletionsCount}
            </span>
          )}
        </div>

        {!activeDiff.isBinary && (
          <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', alignItems: 'center' }}>
            {changeHunks.length > 0 && !isEditing && (
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '2px',
                  background: 'rgba(255, 255, 255, 0.05)',
                  borderRadius: '6px',
                  padding: '2px 4px',
                  border: '1px solid var(--border-color)',
                }}
              >
                <button
                  type="button"
                  className="btn compact-btn btn-secondary"
                  style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                  onClick={onPrevChange}
                  title="Previous Change (Shift+F7 or Alt+Up)"
                >
                  ▲ Prev
                </button>
                <span
                  style={{
                    fontSize: '0.74rem',
                    color: 'var(--text-muted)',
                    fontFamily: 'var(--font-mono)',
                    minWidth: '64px',
                    textAlign: 'center',
                  }}
                >
                  {activeHunkIndex >= 0
                    ? `${activeHunkIndex + 1} / ${changeHunks.length}`
                    : `${changeHunks.length} diffs`}
                </span>
                <button
                  type="button"
                  className="btn compact-btn btn-secondary"
                  style={{ padding: '2px 6px', fontSize: '0.74rem' }}
                  onClick={onNextChange}
                  title="Next Change (F7 or Alt+Down)"
                >
                  ▼ Next
                </button>
              </div>
            )}
            <button
              type="button"
              className={`btn compact-btn ${isWordWrap ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '2px 8px', fontSize: '0.74rem' }}
              onClick={onToggleWordWrap}
              title="Toggle Word-Wrap / Line-Break for code lines"
            >
              {isWordWrap ? '↩ Wrap: ON' : '➡ Wrap: OFF'}
            </button>
            <button
              type="button"
              className={`btn compact-btn ${viewMode === 'sideBySide' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '2px 8px', fontSize: '0.74rem' }}
              onClick={() => onSetViewMode('sideBySide')}
            >
              📖 Side-by-Side
            </button>
            <button
              type="button"
              className={`btn compact-btn ${viewMode === 'unified' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '2px 8px', fontSize: '0.74rem' }}
              onClick={() => onSetViewMode('unified')}
            >
              📜 In-Line Diff
            </button>
            <button
              type="button"
              className="btn compact-btn btn-secondary"
              style={{ padding: '2px 8px', fontSize: '0.74rem' }}
              onClick={() => onSetFullscreen(true)}
              title="Full Screen View"
            >
              ⛶ Full Screen
            </button>
          </div>
        )}
      </div>

      {/* Sub-view switcher for Side-by-Side modified files */}
      {!activeDiff.isBinary && viewMode === 'sideBySide' && isModified && (
        <div className="diff-mobile-subnav" style={{ display: 'flex', gap: '6px', marginBottom: '8px' }}>
          <button
            type="button"
            className={`btn compact-btn ${sideBySideMobileTab === 'original' ? 'btn-primary' : 'btn-secondary'}`}
            style={{ flex: 1, padding: '2px 6px', fontSize: '0.72rem', justifyContent: 'center' }}
            onClick={() => onSetSideBySideMobileTab('original')}
          >
            Original (Before)
          </button>
          <button
            type="button"
            className={`btn compact-btn ${sideBySideMobileTab === 'modified' ? 'btn-primary' : 'btn-secondary'}`}
            style={{ flex: 1, padding: '2px 6px', fontSize: '0.72rem', justifyContent: 'center' }}
            onClick={() => onSetSideBySideMobileTab('modified')}
          >
            Modified (After)
          </button>
          <button
            type="button"
            className={`btn compact-btn ${sideBySideMobileTab === 'split' ? 'btn-primary' : 'btn-secondary'}`}
            style={{ flex: 1, padding: '2px 6px', fontSize: '0.72rem', justifyContent: 'center' }}
            onClick={() => onSetSideBySideMobileTab('split')}
          >
            Split 50/50
          </button>
        </div>
      )}
    </>
  );
};
