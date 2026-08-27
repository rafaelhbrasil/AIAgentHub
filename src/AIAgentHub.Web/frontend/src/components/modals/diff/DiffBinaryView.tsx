import React from 'react';
import { FileChangeDto } from '../../../types/diff';

interface DiffBinaryViewProps {
  activeDiff: FileChangeDto;
}

export const DiffBinaryView: React.FC<DiffBinaryViewProps> = ({ activeDiff }) => {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
      <div>
        <strong>Original</strong>
        <br />
        <img src={activeDiff.oldContent || ''} alt="Original binary preview" style={{ maxWidth: '100%' }} />
      </div>
      <div>
        <strong>Modified</strong>
        <br />
        <img src={activeDiff.newContent || ''} alt="Modified binary preview" style={{ maxWidth: '100%' }} />
      </div>
    </div>
  );
};
