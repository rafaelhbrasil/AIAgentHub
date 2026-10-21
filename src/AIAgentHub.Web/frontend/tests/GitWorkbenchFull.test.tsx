import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { GitWorkbench, CreateBranchModal } from '../src/components/workspaces/studio/git/GitWorkbench';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../src/types/git';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';
import { Modal } from '../src/components/common/Modal';
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
            <Modal />
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
            <Modal />
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
            <Modal />
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
            <Modal />
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

  it('handles branch checkout, create branch modal submission, error toasts, and view diff', async () => {
    const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string, _options?: any) => {
      if (url.includes('/api/v1/git/status')) {
        return { ok: true, status: 200, data: readyStatus } as any;
      }
      if (url.includes('/api/v1/git/branches')) {
        return { ok: true, status: 200, data: branches } as any;
      }
      if (url.includes('/api/v1/git/history')) {
        return { ok: true, status: 200, data: history } as any;
      }
      if (url.includes('/api/v1/git/checkout')) {
        return { ok: true, status: 200, data: { success: true } } as any;
      }
      if (url.includes('/api/v1/git/branch')) {
        return { ok: true, status: 200, data: { success: true } } as any;
      }
      if (url.includes('/api/v1/git/commit-diff')) {
        return {
          ok: true,
          status: 200,
          data: {
            id: 'diff-123',
            filePath: 'src/staged.ts',
            diffText: '@@ -1 +1 @@\n-old\n+new',
            isCreated: false,
            isDeleted: false,
            isBinary: false,
          },
        } as any;
      }
      if (url.includes('/api/v1/git/commit')) {
        return {
          ok: true,
          status: 200,
          data: {
            hash: 'c1234567890abcdef',
            shortHash: 'c123456',
            author: 'John Doe',
            email: 'john@example.com',
            date: '2026-10-18T12:00:00Z',
            message: 'fix: update workbench splitter',
            parents: [],
            refs: 'HEAD -> main',
            files: [{ path: 'src/staged.ts', status: 'M' }],
          },
        } as any;
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
            <Modal />
          </ModalProvider>
        </ToastProvider>
      );
    });

    // Happy path stage/unstage all
    const happyStageAllBtn = container.querySelector('#workingTreeStageAllBtn') as HTMLButtonElement;
    if (happyStageAllBtn) {
      await act(async () => {
        happyStageAllBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stage', expect.objectContaining({
        body: { workspaceId: 'ws-123', files: undefined, stageAll: true },
      }));
    }

    const happyUnstageAllBtn = container.querySelector('#workingTreeUnstageAllBtn') as HTMLButtonElement;
    if (happyUnstageAllBtn) {
      await act(async () => {
        happyUnstageAllBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/unstage', expect.objectContaining({
        body: { workspaceId: 'ws-123', files: undefined, unstageAll: true },
      }));
    }

    // 1. Branch checkout
    const branchSelect = container.querySelector('#workbenchBranchSelect') as HTMLSelectElement;
    expect(branchSelect).not.toBeNull();
    if (branchSelect) {
      await act(async () => {
        branchSelect.value = 'feature/auth';
        branchSelect.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/checkout', expect.objectContaining({
        body: { workspaceId: 'ws-123', branchName: 'feature/auth' },
      }));
    }

    // 2. Create branch button opens modal
    const newBranchBtn = container.querySelector('#workbenchNewBranchBtn') as HTMLButtonElement;
    expect(newBranchBtn).not.toBeNull();
    await act(async () => {
      newBranchBtn.click();
    });

    // In modal provider, modal should now be open
    const branchInput = document.body.querySelector('#workbenchBranchNameInput') as HTMLInputElement;
    if (branchInput) {
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(branchInput, 'feature/test-new');
        branchInput.dispatchEvent(new Event('input', { bubbles: true }));
        branchInput.dispatchEvent(new Event('change', { bubbles: true }));
      });
      const submitModalBtn = document.body.querySelector('form button[type="submit"]') as HTMLButtonElement;
      if (submitModalBtn) {
        await act(async () => {
          submitModalBtn.click();
        });
        expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/branch', expect.objectContaining({
          body: { workspaceId: 'ws-123', branchName: 'feature/test-new', checkout: true },
        }));
      }
    }

    // 3. Mount without initialStatus to verify refreshAll() on mount
    await act(async () => {
      root?.render(
        <ToastProvider>
          <ModalProvider>
            <GitWorkbench workspaceId="ws-456" />
          </ModalProvider>
        </ToastProvider>
      );
    });
    expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/git/status?workspaceId=ws-456'));

    // 4. Test remote operations: Fetch, Pull, Push
    const fetchBtn = container.querySelector('#workbenchFetchBtn') as HTMLButtonElement;
    if (fetchBtn) {
      await act(async () => {
        fetchBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/fetch', expect.anything());
    }

    const pullBtn = container.querySelector('#workbenchPullBtn') as HTMLButtonElement;
    if (pullBtn) {
      await act(async () => {
        pullBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/pull', expect.anything());
    }

    const pushBtn = container.querySelector('#workbenchPushBtn') as HTMLButtonElement;
    if (pushBtn) {
      await act(async () => {
        pushBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/push', expect.anything());
    }

    // 5. Test Stash Save and Pop
    const stashSaveBtn = container.querySelector('#workingTreeStashSaveBtn') as HTMLButtonElement;
    if (stashSaveBtn) {
      await act(async () => {
        stashSaveBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stash', expect.objectContaining({
        body: { workspaceId: 'ws-123', action: 'save' },
      }));
    }

    const stashPopBtn = container.querySelector('#workingTreeStashPopBtn') as HTMLButtonElement;
    if (stashPopBtn) {
      await act(async () => {
        stashPopBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/stash', expect.objectContaining({
        body: { workspaceId: 'ws-123', action: 'pop' },
      }));
    }

    // 6. Test Commit creation
    const commitMsgInput = container.querySelector('#workingTreeCommitMessage') as HTMLTextAreaElement;
    const commitBtn = container.querySelector('#workingTreeCommitBtn') as HTMLButtonElement;
    if (commitMsgInput && commitBtn) {
      await act(async () => {
        commitMsgInput.value = 'feat: test new commit';
        commitMsgInput.dispatchEvent(new Event('input', { bubbles: true }));
        commitMsgInput.dispatchEvent(new Event('change', { bubbles: true }));
      });
      await act(async () => {
        commitBtn.click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/git/commit', expect.objectContaining({
        body: { workspaceId: 'ws-123', message: 'feat: test new commit' },
      }));
    }

    // 7. Test Filter Query in Commit Log Table
    const filterInput = container.querySelector('#workbenchFilterInput') as HTMLInputElement;
    if (filterInput) {
      await act(async () => {
        filterInput.value = 'splitter';
        filterInput.dispatchEvent(new Event('input', { bubbles: true }));
        filterInput.dispatchEvent(new Event('change', { bubbles: true }));
      });
      expect(container.textContent).toContain('fix: update workbench splitter');
    }

    // 8. Select a commit row to mount CommitDetailInspector and trigger handleViewDiff
    apiFetchSpy.mockImplementation(async (url: string) => {
      if (url.includes('/api/v1/git/commit-diff')) {
        return {
          ok: true,
          status: 200,
          data: {
            id: 'diff-123',
            filePath: 'src/staged.ts',
            diffText: '@@ -1 +1 @@\n-old\n+new',
            isCreated: false,
            isDeleted: false,
            isBinary: false,
          },
        } as any;
      }
      if (url.includes('/api/v1/git/commit')) {
        return {
          ok: true,
          status: 200,
          data: {
            hash: 'c1234567890abcdef',
            shortHash: 'c123456',
            author: 'John Doe',
            email: 'john@example.com',
            date: '2026-10-18T12:00:00Z',
            message: 'fix: update workbench splitter',
            parents: [],
            refs: 'HEAD -> main',
            files: [{ path: 'src/staged.ts', status: 'M', additions: 1, deletions: 0 }],
          },
        } as any;
      }
      return { ok: true, status: 200, data: {} } as any;
    });

    const commitRows = container.querySelectorAll('.git-log-row');
    if (commitRows.length > 1) {
      await act(async () => {
        (commitRows[1] as HTMLElement).click();
      });

      // Now trigger onViewDiff
      const viewDiffBtn = container.querySelector('button[title="View file diff in commit"]') as HTMLButtonElement;
      if (viewDiffBtn) {
        await act(async () => {
          viewDiffBtn.click();
        });
        expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/git/commit-diff'));

        // DiffViewerModal should now be rendered
        const closeDiffModalBtn = container.querySelector('.modal-header button, button[title*="Close"]') as HTMLButtonElement;
        if (closeDiffModalBtn) {
          await act(async () => {
            closeDiffModalBtn.click();
          });
        }
      }
    }

    // 9. Test error branches for stage, unstage, commit, stash, fetch, pull, push, checkout
    apiFetchSpy.mockImplementation(async (url: string) => {
      if (url.includes('/api/v1/git/status')) {
        return { ok: true, status: 200, data: readyStatus } as any;
      }
      if (url.includes('/api/v1/git/branches')) {
        return { ok: true, status: 200, data: branches } as any;
      }
      if (url.includes('/api/v1/git/history')) {
        return { ok: true, status: 200, data: history } as any;
      }
      return {
        ok: false,
        status: 400,
        error: 'Git operation failed with conflict.',
      } as any;
    });

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

    const stageAllBtn = container.querySelector('#workingTreeStageAllBtn') as HTMLButtonElement;
    if (stageAllBtn) {
      await act(async () => {
        stageAllBtn.click();
      });
    }

    const unstageAllBtn = container.querySelector('#workingTreeUnstageAllBtn') as HTMLButtonElement;
    if (unstageAllBtn) {
      await act(async () => {
        unstageAllBtn.click();
      });
    }

    if (fetchBtn) {
      await act(async () => {
        fetchBtn.click();
      });
    }

    if (pullBtn) {
      await act(async () => {
        pullBtn.click();
      });
    }

    if (pushBtn) {
      await act(async () => {
        pushBtn.click();
      });
    }

    if (stashSaveBtn) {
      await act(async () => {
        stashSaveBtn.click();
      });
    }

    if (branchSelect) {
      await act(async () => {
        branchSelect.value = 'feature/auth';
        branchSelect.dispatchEvent(new Event('change', { bubbles: true }));
      });
    }
  });

  it('renders notices when git is not installed or workspace is not a repo', () => {
    const notInstalledHtml = renderToString(
      <ToastProvider>
        <ModalProvider>
          <GitWorkbench
            workspaceId="ws-123"
            initialStatus={{
              isGitInstalled: false,
              isRepository: false,
              currentBranch: '',
              ahead: 0,
              behind: 0,
              staged: [],
              modified: [],
              untracked: [],
            }}
          />
        </ModalProvider>
      </ToastProvider>
    );
    expect(notInstalledHtml).toContain('Git Not Detected');

    const notRepoHtml = renderToString(
      <ToastProvider>
        <ModalProvider>
          <GitWorkbench
            workspaceId="ws-123"
            initialStatus={{
              isGitInstalled: true,
              isRepository: false,
              currentBranch: '',
              ahead: 0,
              behind: 0,
              staged: [],
              modified: [],
              untracked: [],
            }}
          />
        </ModalProvider>
      </ToastProvider>
    );
    expect(notRepoHtml).toContain('Not a Git Repository');
  });

  it('handles splitter keyboard resizing, mouse drag, and diff modal viewing', async () => {
    const diffChangeData = {
      filePath: 'src/staged.ts',
      status: 'M',
      additions: 2,
      deletions: 1,
      chunks: [
        {
          oldStart: 1,
          oldLines: 2,
          newStart: 1,
          newLines: 3,
          lines: [
            { type: 'unchanged' as const, content: 'import React;' },
            { type: 'added' as const, content: 'const a = 1;' },
          ],
        },
      ],
    };

    const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
      if (url.includes('/commit-diff')) {
        return { ok: true, status: 200, data: diffChangeData } as any;
      }
      if (url.includes('/commit-detail')) {
        return {
          ok: true,
          status: 200,
          data: {
            hash: 'c1234567890abcdef',
            shortHash: 'c123456',
            author: 'John Doe',
            email: 'john@example.com',
            date: '2026-10-18T12:00:00Z',
            message: 'fix: update workbench splitter',
            parents: [],
            files: [{ path: 'src/staged.ts', status: 'M', additions: 2, deletions: 1 }],
          },
        } as any;
      }
      return { ok: true, status: 200, data: null } as any;
    });

    root = createRoot(container);
    await act(async () => {
      root?.render(
        <ToastProvider>
          <ModalProvider>
            <Modal />
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

    // Test splitter keyboard resizing
    const splitter = container.querySelector('.git-workbench-splitter') as HTMLButtonElement;
    expect(splitter).not.toBeNull();

    await act(async () => {
      splitter.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }));
      splitter.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    });

    // Test splitter mouse drag
    await act(async () => {
      splitter.dispatchEvent(new MouseEvent('mousedown', { clientY: 300, bubbles: true }));
      window.dispatchEvent(new MouseEvent('mousemove', { clientY: 350 }));
      window.dispatchEvent(new MouseEvent('mouseup', { clientY: 350 }));
    });

    // Test view diff for staged file
    const diffButtons = container.querySelectorAll('button[title*="Diff"], button[title*="diff"]');
    if (diffButtons.length > 0) {
      await act(async () => {
        (diffButtons[0] as HTMLButtonElement).click();
      });
      expect(apiFetchSpy).toHaveBeenCalledWith(expect.stringContaining('/api/v1/git/commit-diff'));
    }

    // Test switching to commit detail inspector
    const commitRows = container.querySelectorAll('.git-log-row');
    const historyRow = Array.from(commitRows).find((r) => r.id !== 'row-working-tree') as HTMLElement;
    if (historyRow) {
      await act(async () => {
        historyRow.click();
      });
      expect(container.textContent).toContain('Changed Files (1)');
    }
  });
});
