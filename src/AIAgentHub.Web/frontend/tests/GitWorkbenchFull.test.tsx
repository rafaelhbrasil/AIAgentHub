import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { GitWorkbench, CreateBranchModal } from '../src/components/workspaces/studio/git/GitWorkbench';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../src/types/git';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';

describe('GitWorkbench Full Component', () => {
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
    const readyStatus: GitStatusDto = {
      isGitInstalled: true,
      isRepository: true,
      currentBranch: 'main',
      ahead: 1,
      behind: 0,
      staged: [{ path: 'src/index.ts', status: 'M', isStaged: true }],
      modified: [],
      untracked: [],
    };

    const branches: GitBranchDto[] = [
      { name: 'main', isCurrent: true, isRemote: false },
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
    ];

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

    // Toolbar and table elements
    expect(html).toContain('id="workbenchBranchSelect"');
    expect(html).toContain('id="workbenchNewBranchBtn"');
    expect(html).toContain('id="workbenchFetchBtn"');
    expect(html).toContain('id="workbenchPullBtn"');
    expect(html).toContain('id="workbenchPushBtn"');
    expect(html).toContain('id="workbenchRefreshBtn"');

    // Inspector pane renders WorkingTreeInspector by default (selectedRowId="working-tree")
    expect(html).toContain('git-working-tree-inspector');
    expect(html).toContain('src/index.ts');
    expect(html).toContain('id="workbenchCommitMessageInput"');
    expect(html).toContain('id="workbenchCommitSubmitBtn"');
  });

  it('renders CreateBranchModal with input and action buttons', () => {
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
