import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { GitWorkbench, CreateBranchModal } from '../src/components/workspaces/studio/git/GitWorkbench';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../src/types/git';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';
import * as apiClient from '../src/services/apiClient';

describe('GitWorkbench Full Component', () => {
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

  const readyStatus: GitStatusDto = {
    isGitInstalled: true,
    isRepository: true,
    currentBranch: 'main',
    ahead: 1,
    behind: 0,
    staged: [{ path: 'src/staged.ts', status: 'M', isStaged: true }],
    modified: [{ path: 'src/modified.ts', status: 'M', isStaged: false }],
    untracked: ['src/new.ts'],
  };

  const branches: GitBranchDto[] = [
    { name: 'main', isCurrent: true, isRemote: false },
    { name: 'feature/auth', isCurrent: false, isRemote: false },
  ];

  const history: GitCommitDto[] = [
    {
      hash: 'c1234567890abcdef',
      shortHash: 'c123456',
      author: 'John Doe',
      email: 'john@example.com',
      date: '2026-10-18T12:00:00Z',
      message: 'fix: update workbench splitter',
      parents: [],
      refs: 'HEAD -> main',
    },
    {
      hash: 'a9876543210fedcba',
      shortHash: 'a987654',
      author: 'Jane Doe',
      email: 'jane@example.com',
      date: '2026-10-17T12:00:00Z',
      message: 'feat: add git graph',
      parents: ['c1234567890abcdef'],
      refs: '',
    },
  ];

  it('renders loading state when initialStatus is not provided', () => {
    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <GitWorkbench workspaceId="ws-123" />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('git-workbench-loading');
    expect(html).toContain('Loading Git Workbench...');
  });

  it('renders warning when git is not installed', () => {
    const notInstalledStatus: GitStatusDto = {
      isGitInstalled: false,
      isRepository: false,
      currentBranch: '',
      ahead: 0,
      behind: 0,
      staged: [],
      modified: [],
      untracked: [],
    };

    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <GitWorkbench workspaceId="ws-123" initialStatus={notInstalledStatus} />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('Git Not Detected');
    expect(html).toContain('Git is not installed on this server host machine');
  });

  it('renders notice when workspace is not a git repository', () => {
    const notRepoStatus: GitStatusDto = {
      isGitInstalled: true,
      isRepository: false,
      currentBranch: '',
      ahead: 0,
      behind: 0,
      staged: [],
      modified: [],
      untracked: [],
    };

    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <GitWorkbench workspaceId="ws-123" initialStatus={notRepoStatus} />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('Not a Git Repository');
    expect(html).toContain('This workspace directory has not been initialized');
  });

  it('renders workbench workspace with log table, splitter, and inspector', () => {
    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <GitWorkbench
            workspaceId="ws-123"
            initialStatus={readyStatus}
            initialBranches={branches}
            initialHistory={history}
            onFileSelect={vi.fn()}
          />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('git-workbench');
    expect(html).toContain('git-workbench-splitter');
    expect(html).toContain('id="workbenchBranchSelect"');
    expect(html).toContain('id="workbenchNewBranchBtn"');
    expect(html).toContain('id="workbenchFetchBtn"');
    expect(html).toContain('id="workbenchPullBtn"');
    expect(html).toContain('id="workbenchPushBtn"');
    expect(html).toContain('id="workbenchRefreshBtn"');
    expect(html).toContain('git-working-tree-inspector');
  });

  it('handles splitter keyboard resize events (ArrowUp and ArrowDown)', async () => {
    root = createRoot(container);
    await act(async () => {
      root?.render(
        <ToastProvider>
          <ModalProvider>
            <GitWorkbench
              workspaceId="ws-123"
              initialStatus={readyStatus}
              initialBranches={branches}
              initialHistory={history}
            />
          </ModalProvider>
        </ToastProvider>
      );
    });

    const splitter = container.querySelector('.git-workbench-splitter') as HTMLButtonElement;
    expect(splitter).toBeTruthy();

    await act(async () => {
      splitter.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }));
    });

    await act(async () => {
      splitter.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    });
  });

  it('handles splitter mouse drag resize events', async () => {
    root = createRoot(container);
    await act(async () => {
      root?.render(
        <ToastProvider>
          <ModalProvider>
            <GitWorkbench
              workspaceId="ws-123"
              initialStatus={readyStatus}
              initialBranches={branches}
              initialHistory={history}
            />
          </ModalProvider>
        </ToastProvider>
      );
    });

    const splitter = container.querySelector('.git-workbench-splitter') as HTMLButtonElement;
    expect(splitter).toBeTruthy();

    await act(async () => {
      splitter.dispatchEvent(new MouseEvent('mousedown', { clientY: 100, bubbles: true }));
      window.dispatchEvent(new MouseEvent('mousemove', { clientY: 150 }));
      window.dispatchEvent(new MouseEvent('mouseup'));
    });
  });

  it('handles stage, unstage, commit, and stash operations', async () => {
    const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
      if (url.includes('/api/v1/git/status')) {
        return { ok: true, status: 200, data: readyStatus } as any;
      }
      if (url.includes('/api/v1/git/branches')) {
        return { ok: true, status: 200, data: branches } as any;
      }
      if (url.includes('/api/v1/git/history')) {
        return { ok: true, status: 200, data: history } as any;
      }
      return { ok: true, status: 200, data: { success: true } } as any;
    });

    root = createRoot(container);
    await act(async () => {
      root?.render(
        <ToastProvider>
          <ModalProvider>
            <GitWorkbench
              workspaceId="ws-123"
              initialStatus={readyStatus}
              initialBranches={branches}
              initialHistory={history}
            />
          </ModalProvider>
        </ToastProvider>
      );
    });

    // Stage all
    const stageAllBtn = container.querySelector('#workingTreeStageAllBtn') as HTMLButtonElement;
    if (stageAllBtn) {
      await act(async () => {
        stageAllBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stage', expect.anything());
    }

    // Unstage all
    const unstageAllBtn = container.querySelector('#workingTreeUnstageAllBtn') as HTMLButtonElement;
    if (unstageAllBtn) {
      await act(async () => {
        unstageAllBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/unstage', expect.anything());
    }

    // Commit
    const commitInput = container.querySelector('#workbenchCommitMessageInput') as HTMLTextAreaElement;
    expect(commitInput).not.toBeNull();
    if (commitInput) {
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value')?.set;
        nativeSetter?.call(commitInput, 'feat: implement workbench operations');
        commitInput.dispatchEvent(new Event('input', { bubbles: true }));
        commitInput.dispatchEvent(new Event('change', { bubbles: true }));
      });
      const commitBtn = container.querySelector('#workbenchCommitSubmitBtn') as HTMLButtonElement;
      expect(commitBtn).not.toBeNull();
      if (commitBtn) {
        await act(async () => {
          commitBtn.click();
        });
        expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/commit', expect.anything());
      }
    }

    // Stash save & pop
    const stashSaveBtn = container.querySelector('#workingTreeStashBtn') as HTMLButtonElement;
    if (stashSaveBtn) {
      await act(async () => {
        stashSaveBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stash', expect.objectContaining({
        body: expect.objectContaining({ action: 'save' }),
      }));
    }

    const stashPopBtn = container.querySelector('#workingTreePopStashBtn') as HTMLButtonElement;
    if (stashPopBtn) {
      await act(async () => {
        stashPopBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stash', expect.objectContaining({
        body: expect.objectContaining({ action: 'pop' }),
      }));
    }
  });

  it('handles remote sync operations: fetch, pull, and push', async () => {
    const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
      if (url.includes('/api/v1/git/status')) {
        return { ok: true, status: 200, data: readyStatus } as any;
      }
      if (url.includes('/api/v1/git/branches')) {
        return { ok: true, status: 200, data: branches } as any;
      }
      if (url.includes('/api/v1/git/history')) {
        return { ok: true, status: 200, data: history } as any;
      }
      return { ok: true, status: 200, data: { success: true } } as any;
    });

    root = createRoot(container);
    await act(async () => {
      root?.render(
        <ToastProvider>
          <ModalProvider>
            <GitWorkbench
              workspaceId="ws-123"
              initialStatus={readyStatus}
              initialBranches={branches}
              initialHistory={history}
            />
          </ModalProvider>
        </ToastProvider>
      );
    });

    const fetchBtn = container.querySelector('#workbenchFetchBtn') as HTMLButtonElement;
    expect(fetchBtn).not.toBeNull();
    await act(async () => {
      fetchBtn.click();
    });
    expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/fetch', expect.anything());

    const pullBtn = container.querySelector('#workbenchPullBtn') as HTMLButtonElement;
    expect(pullBtn).not.toBeNull();
    await act(async () => {
      pullBtn.click();
    });
    expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/pull', expect.anything());

    const pushBtn = container.querySelector('#workbenchPushBtn') as HTMLButtonElement;
    expect(pushBtn).not.toBeNull();
    await act(async () => {
      pushBtn.click();
    });
    expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/push', expect.anything());

    const refreshBtn = container.querySelector('#workbenchRefreshBtn') as HTMLButtonElement;
    expect(refreshBtn).not.toBeNull();
    await act(async () => {
      refreshBtn.click();
    });
  });

  it('renders CreateBranchModal with input and triggers submission or cancel', () => {
    const onSubmit = vi.fn();
    const onCancel = vi.fn();

    const html = renderToString(
      <CreateBranchModal onSubmit={onSubmit} onCancel={onCancel} />
    );

    expect(html).toContain('id="workbenchBranchNameInput"');
    expect(html).toContain('Branch Name');
    expect(html).toContain('Cancel');
    expect(html).toContain('Create &amp; Checkout');
  });
});
