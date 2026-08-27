import { describe, it, expect } from 'vitest';
import {
  getChangeHunks,
  formatChangeType,
  isCreatedDiff,
  isDeletedDiff,
  isModifiedDiff,
} from '../src/components/modals/diff/diffViewerUtils';
import { DiffChangeType, FileChangeDto } from '../src/types/diff';

describe('diffViewerUtils', () => {
  describe('formatChangeType', () => {
    it('formats Created change types correctly', () => {
      expect(formatChangeType(DiffChangeType.Created)).toBe('Created');
      expect(formatChangeType('Created')).toBe('Created');
    });

    it('formats Deleted change types correctly', () => {
      expect(formatChangeType(DiffChangeType.Deleted)).toBe('Deleted');
      expect(formatChangeType('Deleted')).toBe('Deleted');
    });

    it('formats Modified change types correctly', () => {
      expect(formatChangeType(DiffChangeType.Modified)).toBe('Modified');
      expect(formatChangeType('Modified')).toBe('Modified');
      expect(formatChangeType('unknown' as any)).toBe('Modified');
    });
  });

  describe('type guards', () => {
    const createdDto: FileChangeDto = {
      id: '1',
      conversationId: 'c1',
      relativePath: 'test.txt',
      changeType: DiffChangeType.Created,
      isBinary: false,
    };
    const deletedDto: FileChangeDto = {
      id: '2',
      conversationId: 'c1',
      relativePath: 'test.txt',
      changeType: DiffChangeType.Deleted,
      isBinary: false,
    };
    const modifiedDto: FileChangeDto = {
      id: '3',
      conversationId: 'c1',
      relativePath: 'test.txt',
      changeType: DiffChangeType.Modified,
      isBinary: false,
    };

    it('identifies created, deleted, and modified diffs', () => {
      expect(isCreatedDiff(createdDto)).toBe(true);
      expect(isDeletedDiff(createdDto)).toBe(false);
      expect(isModifiedDiff(createdDto)).toBe(false);

      expect(isCreatedDiff(deletedDto)).toBe(false);
      expect(isDeletedDiff(deletedDto)).toBe(true);
      expect(isModifiedDiff(deletedDto)).toBe(false);

      expect(isCreatedDiff(modifiedDto)).toBe(false);
      expect(isDeletedDiff(modifiedDto)).toBe(false);
      expect(isModifiedDiff(modifiedDto)).toBe(true);

      expect(isCreatedDiff(null)).toBe(false);
      expect(isDeletedDiff(null)).toBe(false);
      expect(isModifiedDiff(null)).toBe(false);
    });
  });

  describe('getChangeHunks', () => {
    it('returns empty array when diff is null or has no lines', () => {
      expect(getChangeHunks(null, 'sideBySide')).toEqual([]);
      expect(getChangeHunks(null, 'unified')).toEqual([]);
    });

    it('detects single and multiple hunks in unified diff lines', () => {
      const diff: FileChangeDto = {
        id: '1',
        conversationId: 'c1',
        relativePath: 'app.ts',
        changeType: DiffChangeType.Modified,
        isBinary: false,
        unifiedLines: [
          { kind: 0, content: 'const a = 1;', oldLineNumber: 1, newLineNumber: 1 }, // unchanged (idx 0)
          { kind: 1, content: 'const b = 2;', oldLineNumber: null, newLineNumber: 2 }, // added (idx 1)
          { kind: 2, content: 'const c = 3;', oldLineNumber: 2, newLineNumber: null }, // deleted (idx 2)
          { kind: 0, content: 'const d = 4;', oldLineNumber: 3, newLineNumber: 3 }, // unchanged (idx 3)
          { kind: 1, content: 'const e = 5;', oldLineNumber: null, newLineNumber: 4 }, // added (idx 4)
        ],
      };

      const hunks = getChangeHunks(diff, 'unified');
      expect(hunks).toHaveLength(2);
      expect(hunks[0]).toEqual({ id: 1, startIndex: 1, endIndex: 2 });
      expect(hunks[1]).toEqual({ id: 2, startIndex: 4, endIndex: 4 });
    });

    it('detects hunks in side-by-side diff lines', () => {
      const diff: FileChangeDto = {
        id: '2',
        conversationId: 'c1',
        relativePath: 'app.ts',
        changeType: DiffChangeType.Modified,
        isBinary: false,
        sideBySideLines: [
          { leftKind: 0, rightKind: 0, leftLineNumber: 1, rightLineNumber: 1, leftText: 'a', rightText: 'a' }, // unchanged (idx 0)
          { leftKind: 2, rightKind: 0, leftLineNumber: 2, rightLineNumber: null, leftText: 'b', rightText: '' }, // deleted on left (idx 1)
          { leftKind: 0, rightKind: 1, leftLineNumber: null, rightLineNumber: 2, leftText: '', rightText: 'b2' }, // added on right (idx 2)
          { leftKind: 0, rightKind: 0, leftLineNumber: 3, rightLineNumber: 3, leftText: 'c', rightText: 'c' }, // unchanged (idx 3)
        ],
      };

      const hunks = getChangeHunks(diff, 'sideBySide');
      expect(hunks).toHaveLength(1);
      expect(hunks[0]).toEqual({ id: 1, startIndex: 1, endIndex: 2 });
    });
  });
});
