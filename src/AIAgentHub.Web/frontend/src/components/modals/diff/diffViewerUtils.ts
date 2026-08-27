import { FileChangeDto, DiffChangeType } from '../../../types/diff';

export interface ChangeHunk {
  id: number;
  startIndex: number;
  endIndex: number;
}

export const getChangeHunks = (
  diff: FileChangeDto | null,
  mode: 'sideBySide' | 'unified'
): ChangeHunk[] => {
  if (!diff) return [];
  const hunks: ChangeHunk[] = [];
  let currentStart: number | null = null;
  let currentEnd: number | null = null;

  if (mode === 'sideBySide' && diff.sideBySideLines) {
    diff.sideBySideLines.forEach((l, idx) => {
      const isChanged =
        l.leftKind === 1 ||
        l.leftKind === 2 ||
        (l.leftKind as any) === 'Deleted' ||
        (l.leftKind as any) === 'Deletion' ||
        l.rightKind === 1 ||
        l.rightKind === 2 ||
        (l.rightKind as any) === 'Added' ||
        (l.rightKind as any) === 'Addition' ||
        (l.leftLineNumber == null && l.rightLineNumber != null) ||
        (l.leftLineNumber != null && l.rightLineNumber == null);

      if (isChanged) {
        if (currentStart === null) {
          currentStart = idx;
        }
        currentEnd = idx;
      } else {
        if (currentStart !== null && currentEnd !== null) {
          hunks.push({ id: hunks.length + 1, startIndex: currentStart, endIndex: currentEnd });
          currentStart = null;
          currentEnd = null;
        }
      }
    });
  } else if (mode === 'unified' && diff.unifiedLines) {
    diff.unifiedLines.forEach((l, idx) => {
      const isChanged =
        l.kind === 1 ||
        l.kind === 2 ||
        (l.kind as any) === 'Added' ||
        (l.kind as any) === 'Addition' ||
        (l.kind as any) === 'Deleted' ||
        (l.kind as any) === 'Deletion';
      if (isChanged) {
        if (currentStart === null) {
          currentStart = idx;
        }
        currentEnd = idx;
      } else {
        if (currentStart !== null && currentEnd !== null) {
          hunks.push({ id: hunks.length + 1, startIndex: currentStart, endIndex: currentEnd });
          currentStart = null;
          currentEnd = null;
        }
      }
    });
  }

  if (currentStart !== null && currentEnd !== null) {
    hunks.push({ id: hunks.length + 1, startIndex: currentStart, endIndex: currentEnd });
  }

  return hunks;
};

export const formatChangeType = (type: DiffChangeType | string): string => {
  if (type === DiffChangeType.Created || type === 'Created') return 'Created';
  if (type === DiffChangeType.Deleted || type === 'Deleted') return 'Deleted';
  return 'Modified';
};

export const isCreatedDiff = (diff: FileChangeDto | null): boolean => {
  if (!diff) return false;
  return diff.changeType === DiffChangeType.Created || (diff.changeType as any) === 'Created';
};

export const isDeletedDiff = (diff: FileChangeDto | null): boolean => {
  if (!diff) return false;
  return diff.changeType === DiffChangeType.Deleted || (diff.changeType as any) === 'Deleted';
};

export const isModifiedDiff = (diff: FileChangeDto | null): boolean => {
  if (!diff) return false;
  return !isCreatedDiff(diff) && !isDeletedDiff(diff);
};
