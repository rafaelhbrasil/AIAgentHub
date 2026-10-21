import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import {
  StudioGitPanel,
  GitNotReadyNotice,
  CreateBranchModal,
  GitBranchControls,
  GitChangesView,
  GitHistoryView,
} from '../src/components/workspaces/studio/StudioGitPanel';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../src/types/git';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';
import { Modal } from '../src/components/common/Modal';
import * as apiClient from '../src/services/apiClient';

describe('StudioGitPanel and Subcomponents', () => {
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

  const mockStatus: GitStatusDto = {
    isGitInstalled: true,
    isRepository: true,
    currentBranch: 'main',
    ahead: 2,
    behind: 1,
    staged: [{ path: 'staged.ts', status: 'M', isStaged: true }],
    modified: [{ path: 'modified.ts', status: 'M', isStaged: false }],
    untracked: ['untracked.ts'],
  };

  const mockBranches: GitBranchDto[] = [
    { name: 'main', isCurrent: true, isRemote: false },
    { name: 'feature/new-ui', isCurrent: false, isRemote: false },
  ];

  const mockHistory: GitCommitDto[] = [
    {
      hash: 'c1234567890abcdef',
      shortHash: 'c123456',
      author: 'Alice',
      email: 'alice@example.com',
      date: '2026-10-18T10:00:00Z',
      message: 'refactor: simplify git panel',
      parents: [],
      refs: null,
    },
  ];

  describe('GitNotReadyNotice', () => {
    it('renders warning when git is not installed', () => {
      const status: GitStatusDto = {
        isGitInstalled: false,
        isRepository: false,
        currentBranch: '',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(<GitNotReadyNotice status={status} />);
      expect(html).toContain('Git Not Detected');
      expect(html).toContain('not available on PATH');
    });

    it('renders warning when folder is not a git repository', () => {
      const status: GitStatusDto = {
        isGitInstalled: true,
        isRepository: false,
        currentBranch: '',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(<GitNotReadyNotice status={status} />);
      expect(html).toContain('Not a Git Repository');
    });

    it('renders null when repository is ready', () => {
      const html = renderToString(<GitNotReadyNotice status={mockStatus} />);
      expect(html).toBe('');
    });
  });

  describe('CreateBranchModal Form', () => {
    it('renders branch creation modal form', () => {
      const html = renderToString(
        <CreateBranchModal onSubmit={vi.fn()} onCancel={vi.fn()} />
      );

      expect(html).toContain('Branch Name');
      expect(html).toContain('gitModalBranchNameInput');
      expect(html).toContain('Create &amp; Checkout');
      expect(html).toContain('Cancel');
    });

    it('submits valid branch name and handles cancellation', async () => {
      const onSubmit = vi.fn();
      const onCancel = vi.fn();

      root = createRoot(container);
      await act(async () => {
        root?.render(<CreateBranchModal onSubmit={onSubmit} onCancel={onCancel} />);
      });

      const input = container.querySelector('#gitModalBranchNameInput') as HTMLInputElement;
      const submitBtn = container.querySelector('button[type="submit"]') as HTMLButtonElement;
      const cancelBtn = container.querySelector('button.btn-secondary') as HTMLButtonElement;

      expect(submitBtn.disabled).toBe(true);

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'feature/test-branch');
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      expect(submitBtn.disabled).toBe(false);

      await act(async () => {
        submitBtn.click();
      });
      expect(onSubmit).toHaveBeenCalledWith('feature/test-branch');

      await act(async () => {
        cancelBtn.click();
      });
      expect(onCancel).toHaveBeenCalled();
    });
  });

  describe('GitBranchControls', () => {
    it('renders branch dropdown, ahead/behind badges, and action buttons', () => {
      const html = renderToString(
        <GitBranchControls
          status={mockStatus}
          branches={mockBranches}
          isExecuting={false}
          onCheckoutBranch={vi.fn()}
          onCreateBranch={vi.fn()}
          onFetch={vi.fn()}
          onPull={vi.fn()}
          onPush={vi.fn()}
        />
      );

      expect(html).toContain('id="gitBranchSelect"');
      expect(html).toContain('id="gitNewBranchBtn"');
      expect(html).toContain('ahead');
      expect(html).toContain('2');
      expect(html).toContain('behind');
      expect(html).toContain('1');
      expect(html).toContain('id="gitFetchBtn"');
      expect(html).toContain('id="gitPullBtn"');
      expect(html).toContain('id="gitPushBtn"');
    });

    it('handles checkout, new branch, fetch, pull, and push button clicks', async () => {
      const onCheckout = vi.fn();
      const onCreate = vi.fn();
      const onFetch = vi.fn();
      const onPull = vi.fn();
      const onPush = vi.fn();

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <GitBranchControls
            status={mockStatus}
            branches={mockBranches}
            isExecuting={false}
            onCheckoutBranch={onCheckout}
            onCreateBranch={onCreate}
            onFetch={onFetch}
            onPull={onPull}
            onPush={onPush}
          />
        );
      });

      const select = container.querySelector('#gitBranchSelect') as HTMLSelectElement;
      await act(async () => {
        select.value = 'feature/new-ui';
        select.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(onCheckout).toHaveBeenCalledWith('feature/new-ui');

      const newBtn = container.querySelector('#gitNewBranchBtn') as HTMLButtonElement;
      await act(async () => {
        newBtn.click();
      });
      expect(onCreate).toHaveBeenCalled();

      const fetchBtn = container.querySelector('#gitFetchBtn') as HTMLButtonElement;
      await act(async () => {
        fetchBtn.click();
      });
      expect(onFetch).toHaveBeenCalled();

      const pullBtn = container.querySelector('#gitPullBtn') as HTMLButtonElement;
      await act(async () => {
        pullBtn.click();
      });
      expect(onPull).toHaveBeenCalled();

      const pushBtn = container.querySelector('#gitPushBtn') as HTMLButtonElement;
      await act(async () => {
        pushBtn.click();
      });
      expect(onPush).toHaveBeenCalled();
    });
  });

  describe('GitChangesView and File Select Actions', () => {
    it('handles file selection, stage/unstage clicks, and commit message changes', async () => {
      const onSelect = vi.fn();
      const onStage = vi.fn();
      const onUnstage = vi.fn();
      const onCommit = vi.fn();
      const onStash = vi.fn();
      const onMsgChange = vi.fn();

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <GitChangesView
            status={mockStatus}
            commitMessage="test message"
            isExecuting={false}
            onCommitMessageChange={onMsgChange}
            onCommit={onCommit}
            onStage={onStage}
            onUnstage={onUnstage}
            onStash={onStash}
            onFileSelect={onSelect}
          />
        );
      });

      // Click modified file to select
      const modFileBtn = Array.from(container.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('modified.ts')
      );
      expect(modFileBtn).toBeDefined();
      await act(async () => {
        modFileBtn?.click();
      });
      expect(onSelect).toHaveBeenCalledWith('modified.ts');

      // Click stage button for modified file
      const stageSingleBtn = modFileBtn?.parentElement?.querySelector('button[title="Stage this file"]') as HTMLButtonElement;
      expect(stageSingleBtn).not.toBeNull();
      await act(async () => {
        stageSingleBtn.click();
      });
      expect(onStage).toHaveBeenCalledWith(['modified.ts']);

      // Click unstage button for staged file
      const stagedFileBtn = Array.from(container.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('staged.ts')
      );
      const unstageSingleBtn = stagedFileBtn?.parentElement?.querySelector('button[title="Unstage this file"]') as HTMLButtonElement;
      expect(unstageSingleBtn).not.toBeNull();
      await act(async () => {
        unstageSingleBtn.click();
      });
      expect(onUnstage).toHaveBeenCalledWith(['staged.ts']);

      // Click Stash buttons
      const stashBtn = container.querySelector('#gitStashBtn') as HTMLButtonElement;
      await act(async () => {
        stashBtn.click();
      });
      expect(onStash).toHaveBeenCalledWith('save');

      const popStashBtn = container.querySelector('#gitPopStashBtn') as HTMLButtonElement;
      await act(async () => {
        popStashBtn.click();
      });
      expect(onStash).toHaveBeenCalledWith('pop');

      // Commit button
      const commitBtn = container.querySelector('#gitCommitBtn') as HTMLButtonElement;
      await act(async () => {
        commitBtn.click();
      });
      expect(onCommit).toHaveBeenCalled();
    });
  });

  describe('GitHistoryView', () => {
    it('renders commit card list and fallback on empty', () => {
      const htmlWithCommits = renderToString(<GitHistoryView history={mockHistory} />);
      expect(htmlWithCommits).toContain('c123456');
      expect(htmlWithCommits).toContain('refactor: simplify git panel');
      expect(htmlWithCommits).toContain('Alice');

      const htmlEmpty = renderToString(<GitHistoryView history={[]} />);
      expect(htmlEmpty).toContain('No commit history found.');
    });
  });

  describe('Interactive StudioGitPanel Drawer Lifecycle', () => {
    it('fetches initial git data on mount, handles drawer close and refresh', async () => {
      const onClose = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
        if (url.includes('/status')) return { ok: true, status: 200, data: mockStatus } as any;
        if (url.includes('/branches')) return { ok: true, status: 200, data: mockBranches } as any;
        if (url.includes('/history')) return { ok: true, status: 200, data: mockHistory } as any;
        return { ok: true, status: 200, data: null } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <StudioGitPanel
                workspaceId="ws-456"
                isOpen={true}
                onClose={onClose}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/git/status'));
      expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/git/branches'));
      expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/git/history'));

      // Test close button
      const closeBtn = container.querySelector('#gitCloseDrawerBtn') as HTMLButtonElement;
      expect(closeBtn).not.toBeNull();
      await act(async () => {
        closeBtn.click();
      });
      expect(onClose).toHaveBeenCalled();

      // Test refresh button
      const refreshBtn = container.querySelector('#gitRefreshBtn') as HTMLButtonElement;
      expect(refreshBtn).not.toBeNull();
      await act(async () => {
        refreshBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledTimes(6); // 3 on mount + 3 on refresh
    });

    it('switches between Changes and History tabs and triggers commit', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string, opts?: any) => {
        if (opts?.method === 'POST' && url.includes('/commit')) {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        if (url.includes('/status')) return { ok: true, status: 200, data: mockStatus } as any;
        if (url.includes('/branches')) return { ok: true, status: 200, data: mockBranches } as any;
        if (url.includes('/history')) return { ok: true, status: 200, data: mockHistory } as any;
        return { ok: true, status: 200, data: null } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <StudioGitPanel
                workspaceId="ws-456"
                isOpen={true}
                onClose={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // Switch to History tab
      const tabButtons = container.querySelectorAll('.studio-git-drawer-content .btn.compact-btn');
      const historyTabBtn = Array.from(tabButtons).find((b) => b.textContent?.includes('History')) as HTMLButtonElement;
      expect(historyTabBtn).toBeDefined();

      await act(async () => {
        historyTabBtn.click();
      });

      expect(container.textContent).toContain('refactor: simplify git panel');

      // Switch back to Changes tab
      const changesTabBtn = Array.from(tabButtons).find((b) => b.textContent?.includes('Changes')) as HTMLButtonElement;
      await act(async () => {
        changesTabBtn.click();
      });

      // Type commit message and commit
      const commitInput = container.querySelector('#gitCommitMessageInput') as HTMLTextAreaElement;
      expect(commitInput).not.toBeNull();
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value')?.set;
        nativeSetter?.call(commitInput, 'feat: add git integration test');
        commitInput.dispatchEvent(new Event('input', { bubbles: true }));
        commitInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const commitBtn = container.querySelector('#gitCommitBtn') as HTMLButtonElement;
      expect(commitBtn.disabled).toBe(false);

      await act(async () => {
        commitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/commit', {
        method: 'POST',
        body: { workspaceId: 'ws-456', message: 'feat: add git integration test' },
      });
    });

    it('handles branch creation, checkout, fetch, pull, and push from drawer', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string, opts?: any) => {
        if (opts?.method === 'POST') {
          return { ok: true, status: 200, data: { success: true } } as any;
        }
        if (url.includes('/status')) return { ok: true, status: 200, data: mockStatus } as any;
        if (url.includes('/branches')) return { ok: true, status: 200, data: mockBranches } as any;
        if (url.includes('/history')) return { ok: true, status: 200, data: mockHistory } as any;
        return { ok: true, status: 200, data: null } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <StudioGitPanel
                workspaceId="ws-456"
                isOpen={true}
                onClose={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // New branch modal
      const newBranchBtn = container.querySelector('#gitNewBranchBtn') as HTMLButtonElement;
      await act(async () => {
        newBranchBtn.click();
      });

      const modalBox = document.querySelector('#modalBox');
      const input = modalBox?.querySelector('#gitModalBranchNameInput') as HTMLInputElement;
      expect(input).not.toBeNull();

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'feature/checkout-test');
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const submitBtn = modalBox?.querySelector('button[type="submit"]') as HTMLButtonElement;
      await act(async () => {
        submitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/branch', {
        method: 'POST',
        body: { workspaceId: 'ws-456', branchName: 'feature/checkout-test', checkout: true },
      });

      // Checkout branch
      const branchSelect = container.querySelector('#gitBranchSelect') as HTMLSelectElement;
      await act(async () => {
        branchSelect.value = 'feature/new-ui';
        branchSelect.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/checkout', {
        method: 'POST',
        body: { workspaceId: 'ws-456', branchName: 'feature/new-ui' },
      });

      // Fetch, pull, push
      const fetchBtn = container.querySelector('#gitFetchBtn') as HTMLButtonElement;
      await act(async () => {
        fetchBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/fetch', {
        method: 'POST',
        body: { workspaceId: 'ws-456' },
      });

      const pullBtn = container.querySelector('#gitPullBtn') as HTMLButtonElement;
      await act(async () => {
        pullBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/pull', {
        method: 'POST',
        body: { workspaceId: 'ws-456' },
      });

      const pushBtn = container.querySelector('#gitPushBtn') as HTMLButtonElement;
      await act(async () => {
        pushBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/push', {
        method: 'POST',
        body: { workspaceId: 'ws-456' },
      });
    });

    it('handles stage, unstage, stash, and pop stash operations with API failures gracefully', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string, opts?: any) => {
        if (opts?.method === 'POST') {
          return { ok: false, status: 500, error: 'Git operation failed.' } as any;
        }
        if (url.includes('/status')) return { ok: true, status: 200, data: mockStatus } as any;
        if (url.includes('/branches')) return { ok: true, status: 200, data: mockBranches } as any;
        if (url.includes('/history')) return { ok: true, status: 200, data: mockHistory } as any;
        return { ok: true, status: 200, data: null } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <StudioGitPanel
                workspaceId="ws-456"
                isOpen={true}
                onClose={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // Stage All
      const stageAllBtn = Array.from(container.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Stage All')
      ) as HTMLButtonElement;
      if (stageAllBtn) {
        await act(async () => {
          stageAllBtn.click();
        });
        expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stage', {
          method: 'POST',
          body: { workspaceId: 'ws-456', files: undefined, stageAll: true },
        });
      }

      // Unstage All
      const unstageAllBtn = Array.from(container.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Unstage All')
      ) as HTMLButtonElement;
      if (unstageAllBtn) {
        await act(async () => {
          unstageAllBtn.click();
        });
        expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/unstage', {
          method: 'POST',
          body: { workspaceId: 'ws-456', files: undefined, unstageAll: true },
        });
      }

      // Stash Save
      const stashBtn = container.querySelector('#gitStashBtn') as HTMLButtonElement;
      await act(async () => {
        stashBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stash', {
        method: 'POST',
        body: { workspaceId: 'ws-456', action: 'save' },
      });

      // Stash Pop
      const popStashBtn = container.querySelector('#gitPopStashBtn') as HTMLButtonElement;
      await act(async () => {
        popStashBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stash', {
        method: 'POST',
        body: { workspaceId: 'ws-456', action: 'pop' },
      });
    });

    it('renders empty notices when git not ready in drawer', async () => {
      vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: {
          isGitInstalled: false,
          isRepository: false,
          currentBranch: '',
          ahead: 0,
          behind: 0,
          staged: [],
          modified: [],
          untracked: [],
        },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <StudioGitPanel
                workspaceId="ws-456"
                isOpen={true}
                onClose={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      expect(container.textContent).toContain('Git Not Detected');
    });
  });
});
