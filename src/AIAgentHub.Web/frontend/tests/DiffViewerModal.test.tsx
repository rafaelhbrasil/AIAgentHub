import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { DiffViewerModal } from '../src/components/modals/DiffViewerModal';
import { DiffSideBySideView } from '../src/components/modals/diff/DiffSideBySideView';
import { FileChangeDto, SideBySideLine } from '../src/types/diff';
import { ToastProvider } from '../src/context/ToastContext';

describe('DiffViewerModal', () => {
  const mockChanges: FileChangeDto[] = [
    {
      id: 'diff-1',
      conversationId: 'conv-1',
      relativePath: 'src/main.ts',
      changeType: 'Modified',
      oldContent: 'console.log("old");',
      newContent: 'console.log("new");',
      isBinary: false,
    },
    {
      id: 'diff-2',
      conversationId: 'conv-1',
      relativePath: 'src/new-file.ts',
      changeType: 'Created',
      oldContent: null,
      newContent: 'const a = 1;',
      isBinary: false,
    },
  ];

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders modal with custom changes in side-by-side and unified views', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    const handleClose = vi.fn();

    await act(async () => {
      root.render(
        <ToastProvider>
          <DiffViewerModal
            workspaceId="ws-123"
            customChanges={mockChanges}
            onClose={handleClose}
          />
        </ToastProvider>
      );
    });

    expect(container.textContent).toContain('Side-by-Side');
    expect(container.textContent).toContain('Accept Changes');
    expect(container.textContent).toContain('Edit Content');

    // Toggle view mode to unified
    const unifiedBtn = Array.from(container.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Unified')
    );
    if (unifiedBtn) {
      await act(async () => {
        unifiedBtn.click();
      });
      expect(container.textContent).toContain('Unified');
    }

    // Toggle Word Wrap
    const wrapBtn = Array.from(container.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Wrap')
    );
    if (wrapBtn) {
      await act(async () => {
        wrapBtn.click();
      });
    }

    // Switch active file
    const secondFile = Array.from(container.querySelectorAll('button, li')).find((el) =>
      el.textContent?.includes('src/new-file.ts')
    );
    if (secondFile) {
      await act(async () => {
        (secondFile as HTMLElement).click();
      });
      expect(container.textContent).toContain('src/new-file.ts');
    }

    // Close
    const closeBtn = container.querySelector('.modal-header button, button[title*="Close"]') as HTMLButtonElement;
    if (closeBtn) {
      await act(async () => {
        closeBtn.click();
      });
      expect(handleClose).toHaveBeenCalled();
    }

    act(() => {
      root.unmount();
    });
    container.remove();
  });

  it('tests DiffSideBySideView variants for created, deleted, mobile and desktop split', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    const lines: SideBySideLine[] = [
      {
        leftLineNumber: 1,
        leftText: 'const x = 1;',
        leftKind: 0,
        rightLineNumber: 1,
        rightText: 'const x = 2;',
        rightKind: 3,
      },
      {
        leftLineNumber: 2,
        leftText: 'const del = true;',
        leftKind: 2,
        rightLineNumber: null,
        rightText: null,
        rightKind: 0,
      },
      {
        leftLineNumber: null,
        leftText: null,
        leftKind: 0,
        rightLineNumber: 2,
        rightText: 'const added = true;',
        rightKind: 1,
      },
    ];

    const onStartEditing = vi.fn();
    const onTopScroll = vi.fn();
    const onBottomScroll = vi.fn();
    const topRef = { current: null } as any;
    const bottomRef = { current: null } as any;

    // 1. isCreated: true with onStartEditing
    await act(async () => {
      root.render(
        <DiffSideBySideView
          sideBySideLines={lines}
          isCreated={true}
          isDeleted={false}
          isMobile={false}
          sideBySideMobileTab="split"
          topPaneRef={topRef}
          bottomPaneRef={bottomRef}
          onTopScroll={onTopScroll}
          onBottomScroll={onBottomScroll}
          onStartEditing={onStartEditing}
        />
      );
    });
    expect(container.textContent).toContain('NEW FILE (CREATED)');
    const editBtnCreated = container.querySelector('button[title*="interactive editing"]') as HTMLButtonElement;
    expect(editBtnCreated).not.toBeNull();
    editBtnCreated?.click();
    expect(onStartEditing).toHaveBeenCalledTimes(1);

    // 2. isDeleted: true
    await act(async () => {
      root.render(
        <DiffSideBySideView
          sideBySideLines={lines}
          isCreated={false}
          isDeleted={true}
          isMobile={false}
          sideBySideMobileTab="split"
          topPaneRef={topRef}
          bottomPaneRef={bottomRef}
          onTopScroll={onTopScroll}
          onBottomScroll={onBottomScroll}
          onStartEditing={onStartEditing}
        />
      );
    });
    expect(container.textContent).toContain('DELETED FILE (ORIGINAL)');

    // 3. isMobile: true with onStartEditing
    await act(async () => {
      root.render(
        <DiffSideBySideView
          sideBySideLines={lines}
          isCreated={false}
          isDeleted={false}
          isMobile={true}
          sideBySideMobileTab="split"
          topPaneRef={topRef}
          bottomPaneRef={bottomRef}
          onTopScroll={onTopScroll}
          onBottomScroll={onBottomScroll}
          onStartEditing={onStartEditing}
        />
      );
    });
    expect(container.textContent).toContain('ORIGINAL (BASELINE)');
    expect(container.textContent).toContain('MODIFIED (CURRENT)');
    const editBtnMobile = container.querySelector('button[title*="interactive editing"]') as HTMLButtonElement;
    expect(editBtnMobile).not.toBeNull();
    editBtnMobile?.click();
    expect(onStartEditing).toHaveBeenCalledTimes(2);

    // 4. Desktop side by side with onStartEditing
    await act(async () => {
      root.render(
        <DiffSideBySideView
          sideBySideLines={lines}
          isCreated={false}
          isDeleted={false}
          isMobile={false}
          sideBySideMobileTab="split"
          topPaneRef={topRef}
          bottomPaneRef={bottomRef}
          onTopScroll={onTopScroll}
          onBottomScroll={onBottomScroll}
          onStartEditing={onStartEditing}
        />
      );
    });
    expect(container.textContent).toContain('ORIGINAL (BASELINE)');
    expect(container.textContent).toContain('MODIFIED (CURRENT)');
    const editBtnDesktop = container.querySelector('button[title*="interactive editing"]') as HTMLButtonElement;
    expect(editBtnDesktop).not.toBeNull();
    editBtnDesktop?.click();
    expect(onStartEditing).toHaveBeenCalledTimes(3);

    act(() => {
      root.unmount();
    });
    container.remove();
  });
});
