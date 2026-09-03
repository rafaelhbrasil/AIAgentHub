import React, { useState, useRef, useEffect } from 'react';
import { FileChangeDto } from '../../../types/diff';
import { useToast } from '../../../context/ToastContext';
import {
  ChangeHunk,
  formatChangeType,
  isCreatedDiff,
  isDeletedDiff,
  isModifiedDiff,
  splitSmartPath,
} from './diffViewerUtils';

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
  onSelectChange: (id: string) => void;
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
  onSelectChange,
  onPrevChange,
  onNextChange,
  onToggleWordWrap,
  onSetViewMode,
  onSetFullscreen,
  onSetSideBySideMobileTab,
}) => {
  let showToast: ((msg: string, type?: any) => void) | undefined;
  try {
    const toast = useToast();
    showToast = toast.showToast;
  } catch {
    showToast = undefined;
  }
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const pathScrollRef = useRef<HTMLDivElement>(null);

  const isCreated = isCreatedDiff(activeDiff);
  const isDeleted = isDeletedDiff(activeDiff);
  const isModified = isModifiedDiff(activeDiff);
  const changeLabel = formatChangeType(activeDiff.changeType);

  const { dir, fileName } = splitSmartPath(activeDiff.relativePath);

  // Auto scroll path to right so filename is visible immediately on load
  useEffect(() => {
    if (pathScrollRef.current) {
      pathScrollRef.current.scrollLeft = pathScrollRef.current.scrollWidth;
    }
  }, [activeDiff.relativePath]);

  // Close dropdown on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsDropdownOpen(false);
      }
    };
    if (isDropdownOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isDropdownOpen]);

  // Close dropdown on Escape
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isDropdownOpen) {
        setIsDropdownOpen(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isDropdownOpen]);

  const handleCopyPath = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (activeDiff.relativePath) {
      if (typeof navigator !== 'undefined' && navigator.clipboard) {
        navigator.clipboard.writeText(activeDiff.relativePath);
      }
      showToast?.('Path copied to clipboard', 'info');
    }
  };

  const renderTopPathRow = () => (
    <div
      ref={dropdownRef}
      className="diff-top-path-container"
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '8px',
        padding: '6px 10px',
        background: 'var(--bg-subtle)',
        border: '1px solid var(--border-color)',
        borderRadius: '6px',
        marginBottom: '8px',
        position: 'relative',
        minWidth: 0,
      }}
    >
      {/* Horizontally scrollable path area (focused on filename) */}
      <div
        ref={pathScrollRef}
        className={`diff-path-scroll-area ${changes.length > 1 ? 'is-clickable' : ''}`}
        onClick={() => {
          if (changes.length > 1) {
            setIsDropdownOpen((prev) => !prev);
          }
        }}
        title={changes.length > 1 ? 'Click to select another file. Swipe/scroll to view full path.' : activeDiff.relativePath}
        style={{
          display: 'flex',
          alignItems: 'baseline',
          gap: '4px',
          overflowX: 'auto',
          whiteSpace: 'nowrap',
          cursor: changes.length > 1 ? 'pointer' : 'default',
          flex: '1 1 auto',
          minWidth: 0,
          paddingRight: '6px',
          scrollbarWidth: 'none',
        }}
      >
        <span style={{ fontSize: '0.82rem', marginRight: '2px', flexShrink: 0 }}>📄</span>
        {dir && (
          <span
            className="diff-path-dir"
            style={{
              color: 'var(--text-muted)',
              fontSize: '0.82rem',
              fontFamily: 'var(--font-mono)',
              whiteSpace: 'nowrap',
              flexShrink: 0,
            }}
          >
            {dir}
          </span>
        )}
        <strong
          className="diff-path-filename"
          style={{
            color: 'var(--text-heading)',
            fontSize: '0.88rem',
            fontFamily: 'var(--font-mono)',
            whiteSpace: 'nowrap',
            fontWeight: 600,
            flexShrink: 0,
          }}
        >
          {fileName}
        </strong>
        {changes.length > 1 && (
          <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)', flexShrink: 0, marginLeft: '4px' }}>
            {isDropdownOpen ? '▴' : '▾'}
          </span>
        )}
      </div>

      {/* Copy Path Icon Button */}
      <button
        type="button"
        onClick={handleCopyPath}
        title="Copy full relative path"
        className="btn btn-secondary compact-btn"
        style={{
          padding: '2px 6px',
          fontSize: '0.74rem',
          flexShrink: 0,
          borderRadius: '4px',
        }}
      >
        📋
      </button>

      {/* Dropdown File Switcher Menu */}
      {isDropdownOpen && (
        <div
          className="diff-files-dropdown-menu"
          style={{
            position: 'absolute',
            top: '100%',
            left: 0,
            marginTop: '4px',
            background: 'var(--bg-card)',
            border: '1px solid var(--border-color)',
            borderRadius: 'var(--radius-sm)',
            boxShadow: 'var(--shadow-card)',
            zIndex: 100,
            maxHeight: '320px',
            overflowY: 'auto',
            width: 'max-content',
            minWidth: '280px',
            maxWidth: '92vw',
            padding: '4px',
            display: 'flex',
            flexDirection: 'column',
            gap: '2px',
          }}
        >
          {changes.map((c, idx) => {
            const itemSmart = splitSmartPath(c.relativePath);
            const isItemCreated = isCreatedDiff(c);
            const isItemDeleted = isDeletedDiff(c);
            const isSelected = c.id === activeDiff.id;
            const itemLabel = formatChangeType(c.changeType);

            return (
              <button
                key={c.id}
                type="button"
                onClick={() => {
                  onSelectChange(c.id);
                  setIsDropdownOpen(false);
                }}
                className="diff-files-dropdown-item"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '12px',
                  padding: '6px 10px',
                  borderRadius: '4px',
                  background: isSelected ? 'rgba(99, 102, 241, 0.15)' : 'transparent',
                  border: isSelected ? '1px solid rgba(99, 102, 241, 0.3)' : '1px solid transparent',
                  cursor: 'pointer',
                  textAlign: 'left',
                  color: isSelected ? 'var(--text-heading)' : 'var(--text-main)',
                  fontSize: '0.82rem',
                  fontFamily: 'var(--font-sans)',
                }}
              >
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'baseline',
                    gap: '4px',
                    overflow: 'hidden',
                    minWidth: 0,
                  }}
                >
                  <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem', flexShrink: 0 }}>
                    {idx + 1}.
                  </span>
                  {itemSmart.dir && (
                    <span
                      style={{
                        color: 'var(--text-muted)',
                        fontSize: '0.76rem',
                        fontFamily: 'var(--font-mono)',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        direction: 'rtl',
                        textAlign: 'left',
                        maxWidth: '140px',
                      }}
                    >
                      <bdo dir="ltr">{itemSmart.dir}</bdo>
                    </span>
                  )}
                  <strong
                    style={{
                      fontFamily: 'var(--font-mono)',
                      fontSize: '0.82rem',
                      fontWeight: 600,
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}
                  >
                    {itemSmart.fileName}
                  </strong>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexShrink: 0 }}>
                  {c.additionsCount !== undefined && c.additionsCount > 0 && (
                    <span style={{ color: '#4ade80', fontSize: '0.74rem', fontWeight: 600 }}>
                      +{c.additionsCount}
                    </span>
                  )}
                  {c.deletionsCount !== undefined && c.deletionsCount > 0 && (
                    <span style={{ color: '#f87171', fontSize: '0.74rem', fontWeight: 600 }}>
                      -{c.deletionsCount}
                    </span>
                  )}
                  <span
                    style={{
                      fontSize: '0.68rem',
                      padding: '1px 5px',
                      borderRadius: '3px',
                      background: isItemCreated
                        ? 'rgba(74, 222, 128, 0.15)'
                        : isItemDeleted
                        ? 'rgba(248, 113, 113, 0.15)'
                        : 'rgba(99, 102, 241, 0.15)',
                      color: isItemCreated ? '#4ade80' : isItemDeleted ? '#f87171' : '#818cf8',
                      fontWeight: 600,
                    }}
                  >
                    {itemLabel}
                  </span>
                  {isSelected && <span style={{ color: '#6366f1', fontSize: '0.85rem' }}>✓</span>}
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );

  const renderMetadataAndNavControls = () => (
    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
      {changes.length > 1 && (
        <div style={{ display: 'flex', gap: '4px', flexShrink: 0 }}>
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            disabled={currentFileIndex <= 0}
            onClick={onPrevFile}
            title="Previous File (Alt+Left)"
            style={{ padding: '3px 8px', fontSize: '0.76rem' }}
          >
            ◀
          </button>
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            disabled={currentFileIndex >= changes.length - 1}
            onClick={onNextFile}
            title="Next File (Alt+Right)"
            style={{ padding: '3px 8px', fontSize: '0.76rem' }}
          >
            ▶
          </button>
        </div>
      )}

      {/* Change Status Badge */}
      <span
        style={{
          fontSize: '0.74rem',
          padding: '2px 8px',
          borderRadius: '4px',
          background: isCreated
            ? 'rgba(74, 222, 128, 0.15)'
            : isDeleted
            ? 'rgba(248, 113, 113, 0.15)'
            : 'rgba(99, 102, 241, 0.15)',
          color: isCreated ? '#4ade80' : isDeleted ? '#f87171' : '#818cf8',
          fontWeight: 600,
          flexShrink: 0,
        }}
      >
        {changeLabel}
      </span>

      {/* Additions / Deletions Counts */}
      {activeDiff.additionsCount !== undefined && activeDiff.additionsCount > 0 && (
        <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.78rem', flexShrink: 0 }}>
          +{activeDiff.additionsCount}
        </span>
      )}
      {activeDiff.deletionsCount !== undefined && activeDiff.deletionsCount > 0 && (
        <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.78rem', flexShrink: 0 }}>
          -{activeDiff.deletionsCount}
        </span>
      )}
    </div>
  );

  if (isFullscreen) {
    return (
      <div style={{ marginBottom: '8px', flexShrink: 0 }}>
        {renderTopPathRow()}

        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            padding: '6px 12px',
            background: 'var(--bg-secondary)',
            borderRadius: '6px',
            border: '1px solid var(--border-color)',
            flexWrap: 'wrap',
            gap: '8px',
          }}
        >
          {renderMetadataAndNavControls()}

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
                  {changeHunks.length > 0
                    ? `${(activeHunkIndex >= 0 ? activeHunkIndex : 0) + 1} / ${changeHunks.length}`
                    : '0 diffs'}
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
      </div>
    );
  }

  return (
    <>
      {/* Row 1: Full-width Path & Dropdown Selector */}
      {renderTopPathRow()}

      {/* Row 2: Metadata, Badges & View Action Buttons */}
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
        {renderMetadataAndNavControls()}

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
                  {changeHunks.length > 0
                    ? `${(activeHunkIndex >= 0 ? activeHunkIndex : 0) + 1} / ${changeHunks.length}`
                    : '0 diffs'}
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
