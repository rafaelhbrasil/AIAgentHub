import React from 'react';
import { SideBySideLine } from '../../../types/diff';

interface DiffSideBySideViewProps {
  sideBySideLines: SideBySideLine[];
  isCreated: boolean;
  isDeleted: boolean;
  isMobile: boolean;
  sideBySideMobileTab: 'modified' | 'original' | 'split';
  topPaneRef: React.RefObject<HTMLDivElement>;
  bottomPaneRef: React.RefObject<HTMLDivElement>;
  onTopScroll: (e: React.UIEvent<HTMLDivElement>) => void;
  onBottomScroll: (e: React.UIEvent<HTMLDivElement>) => void;
  onStartEditing: () => void;
}

export const DiffSideBySideView: React.FC<DiffSideBySideViewProps> = ({
  sideBySideLines,
  isCreated,
  isDeleted,
  isMobile,
  sideBySideMobileTab,
  topPaneRef,
  bottomPaneRef,
  onTopScroll,
  onBottomScroll,
  onStartEditing,
}) => {
  const renderLeftLines = () =>
    sideBySideLines.map((l, i) => {
      const isDel =
        l.leftKind === 2 || (l.leftKind as any) === 'Deleted' || (l.leftKind as any) === 'Deletion';
      const isLeftEmpty = l.leftLineNumber == null;
      return (
        <div
          key={i}
          id={`diff-line-left-${i}`}
          className={`diff-line ${isDel ? 'deleted' : isLeftEmpty ? 'empty' : 'unchanged'}`}
        >
          <span className="diff-line-no">{l.leftLineNumber ?? ''}</span>
          <span className="diff-line-marker">{isDel ? '-' : ' '}</span>
          <span className="diff-line-text">{l.leftText ?? ''}</span>
        </div>
      );
    });

  const renderRightLines = () =>
    sideBySideLines.map((l, i) => {
      const isAdd =
        l.rightKind === 1 || (l.rightKind as any) === 'Added' || (l.rightKind as any) === 'Addition';
      const isRightEmpty = l.rightLineNumber == null;
      return (
        <div
          key={i}
          id={`diff-line-right-${i}`}
          className={`diff-line ${isAdd ? 'added' : isRightEmpty ? 'empty' : 'unchanged'}`}
        >
          <span className="diff-line-no">{l.rightLineNumber ?? ''}</span>
          <span className="diff-line-marker">{isAdd ? '+' : ' '}</span>
          <span className="diff-line-text">{l.rightText ?? ''}</span>
        </div>
      );
    });

  if (sideBySideMobileTab === 'original' || isDeleted) {
    return (
      <div className="diff-desktop-split-pane" style={{ width: '100%', flex: '1 1 100%' }}>
        <div className="diff-pane-sticky-header">
          <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.82rem' }}>
            {isDeleted ? 'DELETED FILE (ORIGINAL)' : 'ORIGINAL (BASELINE)'}
          </span>
        </div>
        <div className="diff-pane-lines">{renderLeftLines()}</div>
      </div>
    );
  }

  if (sideBySideMobileTab === 'modified' || isCreated) {
    return (
      <div className="diff-desktop-split-pane" style={{ width: '100%', flex: '1 1 100%' }}>
        <div
          className="diff-pane-sticky-header"
          style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
        >
          <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
            {isCreated ? 'NEW FILE (CREATED)' : 'MODIFIED (CURRENT)'}
          </span>
          <button
            type="button"
            className="btn compact-btn btn-secondary"
            style={{ padding: '2px 8px', fontSize: '0.72rem' }}
            onClick={onStartEditing}
            title="Toggle interactive editing mode for right pane"
          >
            ✏️ Edit Content
          </button>
        </div>
        <div className="diff-pane-lines">{renderRightLines()}</div>
      </div>
    );
  }

  if (isMobile) {
    return (
      <div className="diff-mobile-stacked-split">
        {/* Top Pane: Original Baseline (BEFORE) */}
        <div ref={topPaneRef} onScroll={onTopScroll} className="diff-mobile-split-pane">
          <div className="diff-pane-sticky-header">
            <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.82rem' }}>
              ORIGINAL (BASELINE)
            </span>
          </div>
          <div className="diff-pane-lines">{renderLeftLines()}</div>
        </div>

        {/* Bottom Pane: Modified Current (AFTER) */}
        <div ref={bottomPaneRef} onScroll={onBottomScroll} className="diff-mobile-split-pane">
          <div
            className="diff-pane-sticky-header"
            style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
          >
            <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
              MODIFIED (CURRENT)
            </span>
            <button
              type="button"
              className="btn compact-btn btn-secondary"
              style={{ padding: '2px 8px', fontSize: '0.72rem' }}
              onClick={onStartEditing}
              title="Toggle interactive editing mode for right pane"
            >
              ✏️ Edit Content
            </button>
          </div>
          <div className="diff-pane-lines">{renderRightLines()}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="diff-desktop-side-by-side">
      {/* Left Pane: Original Baseline (BEFORE) */}
      <div ref={topPaneRef} onScroll={onTopScroll} className="diff-desktop-split-pane">
        <div className="diff-pane-sticky-header">
          <span style={{ color: '#f87171', fontWeight: 600, fontSize: '0.82rem' }}>
            ORIGINAL (BASELINE)
          </span>
        </div>
        <div className="diff-pane-lines">{renderLeftLines()}</div>
      </div>

      {/* Right Pane: Modified Current (AFTER) */}
      <div ref={bottomPaneRef} onScroll={onBottomScroll} className="diff-desktop-split-pane">
        <div
          className="diff-pane-sticky-header"
          style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
        >
          <span style={{ color: '#4ade80', fontWeight: 600, fontSize: '0.82rem' }}>
            MODIFIED (CURRENT)
          </span>
          <button
            type="button"
            className="btn compact-btn btn-secondary"
            style={{ padding: '2px 8px', fontSize: '0.72rem' }}
            onClick={onStartEditing}
            title="Toggle interactive editing mode for right pane"
          >
            ✏️ Edit Content
          </button>
        </div>
        <div className="diff-pane-lines">{renderRightLines()}</div>
      </div>
    </div>
  );
};
