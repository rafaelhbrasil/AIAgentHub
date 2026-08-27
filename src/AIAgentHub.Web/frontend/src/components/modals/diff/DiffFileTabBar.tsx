import React from 'react';
import { FileChangeDto } from '../../../types/diff';
import { formatChangeType } from './diffViewerUtils';

interface DiffFileTabBarProps {
  changes: FileChangeDto[];
  activeChangeId: string | null;
  currentFileIndex: number;
  onSelectChange: (id: string) => void;
  onPrevFile: () => void;
  onNextFile: () => void;
}

export const DiffFileTabBar: React.FC<DiffFileTabBarProps> = ({
  changes,
  activeChangeId,
  currentFileIndex,
  onSelectChange,
  onPrevFile,
  onNextFile,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        marginBottom: '12px',
        overflowX: 'auto',
        paddingBottom: '4px',
      }}
    >
      {changes.length > 1 && (
        <div style={{ display: 'flex', gap: '4px', flexShrink: 0 }}>
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            disabled={currentFileIndex <= 0}
            onClick={onPrevFile}
            title="Previous File (Alt+Left)"
            style={{ padding: '4px 8px', fontSize: '0.78rem' }}
          >
            ◀
          </button>
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            disabled={currentFileIndex >= changes.length - 1}
            onClick={onNextFile}
            title="Next File (Alt+Right)"
            style={{ padding: '4px 8px', fontSize: '0.78rem' }}
          >
            ▶
          </button>
        </div>
      )}
      {changes.map((c) => (
        <button
          key={c.id}
          type="button"
          className={`btn btn-secondary compact-btn ${activeChangeId === c.id ? 'btn-primary' : ''}`}
          onClick={() => onSelectChange(c.id)}
          style={{ fontSize: '0.8rem', padding: '4px 10px', whiteSpace: 'nowrap' }}
          title={c.relativePath}
        >
          {c.relativePath} ({formatChangeType(c.changeType)})
        </button>
      ))}
    </div>
  );
};
