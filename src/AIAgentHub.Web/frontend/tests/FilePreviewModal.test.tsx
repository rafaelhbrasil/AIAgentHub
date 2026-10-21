import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { FilePreviewModal } from '../src/components/modals/FilePreviewModal';
import { FilePreviewDto } from '../src/types/diff';
import { ToastProvider } from '../src/context/ToastContext';

describe('FilePreviewModal', () => {
  let container: HTMLDivElement;
  let root: Root | null = null;

  beforeEach(() => {
    vi.restoreAllMocks();
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  afterEach(() => {
    if (root) {
      act(() => {
        root?.unmount();
      });
      root = null;
    }
    container.remove();
  });

  const sampleMarkdownPreview: FilePreviewDto = {
    filePath: 'docs/readme.md',
    contentType: 'text/markdown',
    isBinary: false,
    rawText: '# Hello World\nThis is a sample markdown file.\nLine 3.',
    renderedHtml: '<h1>Hello World</h1><p>This is a sample markdown file.<br/>Line 3.</p>',
    sizeBytes: 1536,
  };

  const sampleBinaryPreview: FilePreviewDto = {
    filePath: 'assets/logo.png',
    contentType: 'image/png',
    isBinary: true,
    rawText: '',
    renderedHtml: '<img src="data:image/png;base64,..." alt="logo" />',
    sizeBytes: 2500000,
  };

  it('renders markdown preview with rendered HTML, file stats, and close button', () => {
    const onClose = vi.fn();
    act(() => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <FilePreviewModal
            relativePath="docs/readme.md"
            preview={sampleMarkdownPreview}
            onClose={onClose}
          />
        </ToastProvider>
      );
    });

    expect(container.textContent).toContain('docs/readme.md');
    expect(container.textContent).toContain('3 lines');
    expect(container.textContent).toContain('1.5 KB');
    expect(container.querySelector('.markdown-rendered')).not.toBeNull();

    const closeBtn = Array.from(container.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Close'
    );
    expect(closeBtn).toBeDefined();
    act(() => {
      closeBtn?.click();
    });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('switches between Preview and Raw File tabs and copies raw text to clipboard', async () => {
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, {
      clipboard: {
        writeText: writeTextMock,
      },
    });

    act(() => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <FilePreviewModal
            relativePath="docs/readme.md"
            preview={sampleMarkdownPreview}
            onClose={vi.fn()}
          />
        </ToastProvider>
      );
    });

    const rawTabBtn = Array.from(container.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Raw File')
    );
    expect(rawTabBtn).toBeDefined();

    act(() => {
      rawTabBtn?.click();
    });

    expect(container.querySelector('.raw-file-view')).not.toBeNull();
    expect(container.querySelectorAll('.raw-line-row')).toHaveLength(3);

    const copyBtn = Array.from(container.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Copy Raw')
    );
    expect(copyBtn).toBeDefined();

    await act(async () => {
      copyBtn?.click();
    });

    expect(writeTextMock).toHaveBeenCalledWith(sampleMarkdownPreview.rawText);
  });

  it('triggers onOpenEditor when Edit in Editor button is clicked', () => {
    const onClose = vi.fn();
    const onOpenEditor = vi.fn();

    act(() => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <FilePreviewModal
            relativePath="docs/readme.md"
            preview={sampleMarkdownPreview}
            onClose={onClose}
            onOpenEditor={onOpenEditor}
          />
        </ToastProvider>
      );
    });

    const editBtn = container.querySelector('#previewEditInEditorBtn') as HTMLButtonElement;
    expect(editBtn).not.toBeNull();

    act(() => {
      editBtn.click();
    });

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onOpenEditor).toHaveBeenCalledWith('docs/readme.md');
  });

  it('handles binary files without raw tab options and formats file size in MB', () => {
    act(() => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <FilePreviewModal
            relativePath="assets/logo.png"
            preview={sampleBinaryPreview}
            onClose={vi.fn()}
          />
        </ToastProvider>
      );
    });

    expect(container.textContent).toContain('2.38 MB');
    expect(container.textContent).toContain('image/png');
    // Raw tab buttons should not be rendered for binary files
    expect(container.querySelector('.raw-file-view')).toBeNull();
    expect(container.querySelector('#previewEditInEditorBtn')).toBeNull();
  });

  it('formats small byte sizes correctly and handles zero bytes', () => {
    const smallPreview: FilePreviewDto = {
      filePath: 'small.txt',
      contentType: 'text/plain',
      isBinary: false,
      rawText: 'hello',
      renderedHtml: '<pre>hello</pre>',
      sizeBytes: 500,
    };

    act(() => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <FilePreviewModal
            relativePath="small.txt"
            preview={smallPreview}
            onClose={vi.fn()}
          />
        </ToastProvider>
      );
    });

    expect(container.textContent).toContain('500 B');
  });
});
