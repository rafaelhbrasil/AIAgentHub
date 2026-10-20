import { describe, it, expect, vi } from 'vitest';
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

describe('StudioGitPanel and Subcomponents', () => {
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
      const status: GitStatusDto = {
        isGitInstalled: true,
        isRepository: true,
        currentBranch: 'main',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(<GitNotReadyNotice status={status} />);
      expect(html).toBe('');
    });
  });

  describe('CreateBranchModal', () => {
    it('renders branch creation modal form', () => {
      const html = renderToString(
        <CreateBranchModal onSubmit={vi.fn()} onCancel={vi.fn()} />
      );

      expect(html).toContain('Branch Name');
      expect(html).toContain('gitModalBranchNameInput');
      expect(html).toContain('Create &amp; Checkout');
      expect(html).toContain('Cancel');
    });
  });

  describe('StudioGitPanel', () => {
    it('renders drawer header and controls when mounted and open', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <StudioGitPanel
              workspaceId="ws-456"
              isOpen={true}
              onClose={vi.fn()}
            />
          </ModalProvider>
        </ToastProvider>
      );

      expect(html).toContain('studio-git-drawer');
      expect(html).toContain('Git Control');
      expect(html).toContain('gitCloseDrawerBtn');
      expect(html).toContain('gitRefreshBtn');
      expect(html).toContain('Changes');
      expect(html).toContain('History');
      expect(html).toContain('gitCommitBtn');
    });

    it('returns empty string when isOpen is false', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <StudioGitPanel
              workspaceId="ws-456"
              isOpen={false}
              onClose={vi.fn()}
            />
          </ModalProvider>
        </ToastProvider>
      );

      expect(html).toBe('');
    });

    it('adds mobile-hidden class when mobileTab is not git', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <StudioGitPanel
              workspaceId="ws-456"
              isOpen={true}
              mobileTab="sidebar"
              onClose={vi.fn()}
            />
          </ModalProvider>
        </ToastProvider>
      );

      expect(html).toContain('studio-git-drawer');
      expect(html).toContain('mobile-hidden');
    });
  });

  describe('GitBranchControls', () => {
    const mockBranches: GitBranchDto[] = [
      { name: 'main', isCurrent: true, isRemote: false },
      { name: 'feature/new-ui', isCurrent: false, isRemote: false },
    ];
    const mockStatus: GitStatusDto = {
      isGitInstalled: true,
      isRepository: true,
      currentBranch: 'main',
      ahead: 3,
      behind: 1,
      staged: [],
      modified: [],
      untracked: [],
    };

    it('renders branch dropdown and ahead/behind badges', () => {
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
      expect(html).toContain('3');
      expect(html).toContain('behind');
      expect(html).toContain('1');
      expect(html).toContain('id="gitFetchBtn"');
      expect(html).toContain('id="gitPullBtn"');
      expect(html).toContain('id="gitPushBtn"');
    });
  });

  describe('GitChangesView', () => {
    const mockStatus: GitStatusDto = {
      isGitInstalled: true,
      isRepository: true,
      currentBranch: 'main',
      ahead: 0,
      behind: 0,
      staged: [{ path: 'staged.ts', status: 'M', isStaged: true }],
      modified: [{ path: 'modified.ts', status: 'M', isStaged: false }],
      untracked: ['untracked.ts'],
    };

    it('renders staged, modified, and untracked lists with commit box', () => {
      const html = renderToString(
        <GitChangesView
          status={mockStatus}
          commitMessage="wip changes"
          isExecuting={false}
          onCommitMessageChange={vi.fn()}
          onCommit={vi.fn()}
          onStage={vi.fn()}
          onUnstage={vi.fn()}
          onStash={vi.fn()}
        />
      );

      expect(html).toContain('Staged Changes');
      expect(html).toContain('staged.ts');
      expect(html).toContain('Modified Changes');
      expect(html).toContain('modified.ts');
      expect(html).toContain('Untracked');
      expect(html).toContain('untracked.ts');
      expect(html).toContain('id="gitStashBtn"');
      expect(html).toContain('id="gitPopStashBtn"');
      expect(html).toContain('id="gitCommitMessageInput"');
      expect(html).toContain('id="gitCommitBtn"');
    });
  });

  describe('GitHistoryView', () => {
    const mockHistory: GitCommitDto[] = [
      {
        hash: 'c1234567890',
        shortHash: 'c123456',
        author: 'Alice',
        email: 'alice@example.com',
        date: '2026-10-18T10:00:00Z',
        message: 'refactor: simplify git panel',
        parents: [],
        refs: null,
      },
    ];

    it('renders commit card list and fallback on empty', () => {
      const htmlWithCommits = renderToString(<GitHistoryView history={mockHistory} />);
      expect(htmlWithCommits).toContain('c123456');
      expect(htmlWithCommits).toContain('refactor: simplify git panel');
      expect(htmlWithCommits).toContain('Alice');

      const htmlEmpty = renderToString(<GitHistoryView history={[]} />);
      expect(htmlEmpty).toContain('No commit history found.');
    });
  });
});
