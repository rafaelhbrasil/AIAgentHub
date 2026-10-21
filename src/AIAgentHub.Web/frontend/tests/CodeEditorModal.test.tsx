import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderToString } from 'react-dom/server';
import { createRoot } from 'react-dom/client';
import { act } from 'react';
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

    it('mounts interactively, handles file loading, editing, save, find, quick switcher, and diff mode', async () => {
      const { act } = await import('react');
      const { createRoot } = await import('react-dom/client');

      const container = document.createElement('div');
      document.body.appendChild(container);
      const root = createRoot(container);

      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, options?: any) => {
        if (options?.method === 'PUT') {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        return {
          ok: true,
          status: 200,
          data: {
            content: 'console.log("hello");\nconst x = 10;',
            isBinary: false,
          },
        } as any;
      });

      await act(async () => {
        root.render(
          <ToastProvider>
            <CodeEditorModal
              workspaceId="ws-123"
              initialPath="src/test.ts"
              workspaceFiles={['src/test.ts', 'src/other.ts']}
              onClose={vi.fn()}
            />
          </ToastProvider>
        );
      });

      expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/filesystem/file'));

      // Edit content
      const textarea = container.querySelector('textarea') as HTMLTextAreaElement;
      expect(textarea).not.toBeNull();
      if (textarea) {
        await act(async () => {
          const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value')?.set;
          nativeSetter?.call(textarea, 'console.log("updated");\nconst x = 20;');
          textarea.dispatchEvent(new Event('input', { bubbles: true }));
          textarea.dispatchEvent(new Event('change', { bubbles: true }));
        });

        // Trigger Ctrl+S save
        await act(async () => {
          window.dispatchEvent(new KeyboardEvent('keydown', { ctrlKey: true, key: 's' }));
        });
        expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/file', expect.objectContaining({
          method: 'PUT',
        }));

        // Trigger Find toggle
        const findBtn = container.querySelector('#editorFindReplaceBtn') as HTMLButtonElement;
        if (findBtn) {
          await act(async () => {
            findBtn.click();
          });
          expect(container.querySelector('#editorFindInput')).not.toBeNull();
        }

        // Trigger Quick Switcher toggle
        const switcherBtn = container.querySelector('#editorQuickSwitcherBtn') as HTMLButtonElement;
        if (switcherBtn) {
          await act(async () => {
            switcherBtn.click();
          });
        }

        // Trigger Escape to close overlays
        await act(async () => {
          window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
        });

        // Toggle Word Wrap checkbox/label
        const wrapCheckbox = container.querySelector('input[type="checkbox"]') as HTMLInputElement;
        if (wrapCheckbox) {
          await act(async () => {
            wrapCheckbox.click();
          });
        }

        // Toggle Diff button
        const diffBtn = container.querySelector('#editorDiffToggleBtn') as HTMLButtonElement;
        if (diffBtn) {
          await act(async () => {
            diffBtn.click();
          });
          expect(diffBtn.textContent).toContain('Editor View');
        }
      }

      // Test binary file response
      apiFetchSpy.mockResolvedValueOnce({
        ok: true,
        status: 200,
        data: { isBinary: true, content: null },
      } as any);

      await act(async () => {
        root.render(
          <ToastProvider>
            <CodeEditorModal
              workspaceId="ws-123"
              initialPath="assets/image.png"
              workspaceFiles={['assets/image.png']}
              onClose={vi.fn()}
            />
          </ToastProvider>
        );
      });
      expect(container.textContent).toContain('Binary File');

      // Test error response
      apiFetchSpy.mockResolvedValueOnce({
        ok: false,
        status: 404,
        error: 'File does not exist.',
      } as any);

      await act(async () => {
        root.render(
          <ToastProvider>
            <CodeEditorModal
              workspaceId="ws-123"
              initialPath="missing.ts"
              workspaceFiles={['missing.ts']}
              onClose={vi.fn()}
            />
          </ToastProvider>
        );
      });

      act(() => {
        root.unmount();
      });
      container.remove();
    });

    it('handles file save, find/replace, and quick switcher interactions in editor', async () => {
      const container = document.createElement('div');
      document.body.appendChild(container);
      const root = createRoot(container);

      const onSaved = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'PUT') {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        return {
          ok: true,
          status: 200,
          data: { isBinary: false, content: 'const greeting = "hello world";\nconst other = "hello test";' },
        } as any;
      });

      await act(async () => {
        root.render(
          <ToastProvider>
            <CodeEditorModal
              workspaceId="ws-123"
              initialPath="src/app.ts"
              workspaceFiles={['src/app.ts', 'src/util.ts']}
              onClose={vi.fn()}
              onSaved={onSaved}
            />
          </ToastProvider>
        );
      });

      const editorTextarea = container.querySelector('#editorMainTextarea') as HTMLTextAreaElement;
      expect(editorTextarea).not.toBeNull();

      // Edit content
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value')?.set;
        nativeSetter?.call(editorTextarea, 'const greeting = "modified content";');
        editorTextarea.dispatchEvent(new Event('input', { bubbles: true }));
        editorTextarea.dispatchEvent(new Event('change', { bubbles: true }));
      });

      // Save via Ctrl+S
      await act(async () => {
        window.dispatchEvent(new KeyboardEvent('keydown', { ctrlKey: true, key: 's' }));
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/file', expect.objectContaining({
        method: 'PUT',
      }));

      // Test Find & Replace Next and All
      const findBtn = container.querySelector('#editorFindReplaceBtn') as HTMLButtonElement;
      expect(findBtn).not.toBeNull();
      await act(async () => {
        findBtn.click();
      });

      const findInput = container.querySelector('#editorFindInput') as HTMLInputElement;
      const replaceInput = container.querySelector('#editorReplaceInput') as HTMLInputElement;
      expect(findInput).not.toBeNull();

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(findInput, 'content');
        findInput.dispatchEvent(new Event('input', { bubbles: true }));
        nativeSetter?.call(replaceInput, 'universe');
        replaceInput.dispatchEvent(new Event('input', { bubbles: true }));
      });

      const replaceNextBtn = container.querySelector('#editorReplaceNextBtn') as HTMLButtonElement;
      await act(async () => {
        replaceNextBtn.click();
      });
      expect(editorTextarea.value).toContain('universe');

      const replaceAllBtn = container.querySelector('#editorReplaceAllBtn') as HTMLButtonElement;
      await act(async () => {
        replaceAllBtn.click();
      });

      // Test Quick Switcher selection
      const switcherBtn = container.querySelector('#editorQuickSwitcherBtn') as HTMLButtonElement;
      await act(async () => {
        switcherBtn.click();
      });

      const quickFilterInput = container.querySelector('#quickFileFilterInput') as HTMLInputElement;
      expect(quickFilterInput).not.toBeNull();

      const fileButtons = container.querySelectorAll('button');
      const utilBtn = Array.from(fileButtons).find((b) => b.textContent?.includes('util.ts'));
      if (utilBtn) {
        await act(async () => {
          utilBtn.click();
        });
      }

      // Test ReadOnly toggle
      const readOnlyCheckbox = container.querySelector('#editorReadOnlyCheckbox') as HTMLInputElement;
      if (readOnlyCheckbox) {
        await act(async () => {
          readOnlyCheckbox.click();
        });
      }

      // Test Keyboard Shortcuts: Escape
      await act(async () => {
        window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      });

      act(() => {
        root.unmount();
      });
      container.remove();
    });
  });
});
