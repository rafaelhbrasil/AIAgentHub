import React from 'react';
import { UnifiedLine } from '../../../types/diff';

interface DiffUnifiedViewProps {
  unifiedLines: UnifiedLine[];
  isWordWrap: boolean;
}

export const DiffUnifiedView: React.FC<DiffUnifiedViewProps> = ({ unifiedLines, isWordWrap }) => {
  return (
    <div className="diff-unified">
      {unifiedLines.map((l, i) => {
        const isAdd = l.kind === 1 || (l.kind as any) === 'Added' || (l.kind as any) === 'Addition';
        const isDel = l.kind === 2 || (l.kind as any) === 'Deleted' || (l.kind as any) === 'Deletion';
        return (
          <div
            key={i}
            id={`diff-line-row-${i}`}
            className={`diff-line ${isAdd ? 'added' : isDel ? 'deleted' : 'unchanged'}`}
            style={{ display: 'flex', gap: '8px', minWidth: isWordWrap ? '0' : 'max-content' }}
          >
            <span
              style={{
                width: '36px',
                flexShrink: 0,
                textAlign: 'right',
                color: 'var(--text-muted)',
                userSelect: 'none',
                opacity: 0.6,
              }}
            >
              {l.oldLineNumber || ''}
            </span>
            <span
              style={{
                width: '36px',
                flexShrink: 0,
                textAlign: 'right',
                color: 'var(--text-muted)',
                userSelect: 'none',
                opacity: 0.6,
              }}
            >
              {l.newLineNumber || ''}
            </span>
            <span
              style={{
                width: '16px',
                flexShrink: 0,
                textAlign: 'center',
                userSelect: 'none',
                fontWeight: 600,
              }}
            >
              {isAdd ? '+' : isDel ? '-' : ' '}
            </span>
            <span className="diff-line-text" style={{ flex: 1 }}>
              {l.content}
            </span>
          </div>
        );
      })}
    </div>
  );
};
