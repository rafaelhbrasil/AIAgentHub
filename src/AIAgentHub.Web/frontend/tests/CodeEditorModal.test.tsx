import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderToString } from 'react-dom/server';
import {
  CodeEditorModal,
  computeSimpleDiff,
  getDiffLineStyle,
  formatMatchCount,
  EditorDiffView,
} from '../src/components/modals/CodeEditorModal';
import { ToastProvider } from '../src/context/ToastContext';
import * as apiClient from '../src/services/apiClient';

describe('CodeEditorModal and Diff Utilities', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe('formatMatchCount', () => {
    it('returns empty string when query is empty', () => {
      expect(formatMatchCount('', 0)).toBe('');
      expect(formatMatchCount('', 5)).toBe('');
    });

    it('formats singular and plural match counts properly', () => {
      expect(formatMatchCount('test', 1)).toBe('1 match');
      expect(formatMatchCount('test', 0)).toBe('0 matches');
      expect(formatMatchCount('test', 4)).toBe('4 matches');
    });
  });

  describe('getDiffLineStyle', () => {
    it('returns correct styling for added, removed, and unchanged lines', () => {
      expect(getDiffLineStyle('added')).toEqual({
        bg: 'rgba(34, 197, 94, 0.15)',
        sign: '+',
        color: '#4ade80',
      });
      expect(getDiffLineStyle('removed')).toEqual({
        bg: 'rgba(239, 68, 68, 0.15)',
        sign: '-',
        color: '#f87171',
      });
      expect(getDiffLineStyle('unchanged')).toEqual({
        bg: 'transparent',
        sign: ' ',
        color: 'inherit',
      });
    });
  });

  describe('computeSimpleDiff', () => {
    it('detects unchanged content', () => {
      const diff = computeSimpleDiff('line1\nline2', 'line1\nline2');
      expect(diff).toHaveLength(2);
      expect(diff.every((d) => d.type === 'unchanged')).toBe(true);
    });

    it('detects lookahead matches and additions', () => {
      const diff = computeSimpleDiff('header\nfooter', 'header\nmiddle1\nmiddle2\nfooter');
      expect(diff.some((d) => d.type === 'added' && d.text === 'middle1')).toBe(true);
      expect(diff.some((d) => d.type === 'added' && d.text === 'middle2')).toBe(true);
      expect(diff.some((d) => d.type === 'unchanged' && d.text === 'footer')).toBe(true);
    });

    it('detects removed lines and replacements', () => {
      const diff = computeSimpleDiff('oldLine1\noldLine2', 'newLine1');
      expect(diff.some((d) => d.type === 'removed')).toBe(true);
      expect(diff.some((d) => d.type === 'added')).toBe(true);
    });

    it('handles empty inputs', () => {
      expect(computeSimpleDiff('', '')).toEqual([
        { type: 'unchanged', text: '', originalLineNum: 1, newLineNum: 1 },
      ]);
      expect(computeSimpleDiff('something', '')).toEqual([
        { type: 'removed', text: 'something', originalLineNum: 1 },
        { type: 'added', text: '', newLineNum: 1 },
      ]);
    });
  });

  describe('EditorDiffView', () => {
    it('renders empty notice when no diff lines exist', () => {
      const html = renderToString(<EditorDiffView diffLines={[]} />);
      expect(html).toContain('No differences from disk.');
    });

    it('renders diff lines with signs and line numbers', () => {
      const diffLines = [
        { type: 'unchanged' as const, text: 'keep this', originalLineNum: 1, newLineNum: 1 },
        { type: 'added' as const, text: 'new line', newLineNum: 2 },
        { type: 'removed' as const, text: 'deleted line', originalLineNum: 2 },
      ];
      const html = renderToString(<EditorDiffView diffLines={diffLines} />);
      expect(html).toContain('keep this');
      expect(html).toContain('new line');
      expect(html).toContain('deleted line');
      expect(html).toContain('+');
      expect(html).toContain('-');
    });
  });

  describe('CodeEditorModal', () => {
    it('renders loading state initially', () => {
      vi.spyOn(apiClient, 'apiFetch').mockImplementation(() => new Promise(() => {}));

      const html = renderToString(
        <ToastProvider>
          <CodeEditorModal
            workspaceId="ws-123"
            initialPath="src/index.ts"
            workspaceFiles={['src/index.ts', 'src/app.ts']}
            onClose={() => {}}
          />
        </ToastProvider>
      );

      expect(html).toContain('code-editor-container');
      expect(html).toContain('src/index.ts');
      expect(html).toContain('Switch File');
      expect(html).toContain('Find');
      expect(html).toContain('Disk Diff');
      expect(html).toContain('Read-only');
    });

    it('renders without switch button when file list is empty', () => {
      vi.spyOn(apiClient, 'apiFetch').mockImplementation(() => new Promise(() => {}));

      const html = renderToString(
        <ToastProvider>
          <CodeEditorModal
            workspaceId="ws-123"
            initialPath="README.md"
            workspaceFiles={[]}
            onClose={() => {}}
          />
        </ToastProvider>
      );

      expect(html).toContain('README.md');
      expect(html).not.toContain('Switch File');
    });
  });
});
