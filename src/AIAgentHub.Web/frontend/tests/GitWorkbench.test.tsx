import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { GitCommitGraphCell } from '../src/components/workspaces/studio/git/GitCommitGraphCell';
import { GitCommitLogTable } from '../src/components/workspaces/studio/git/GitCommitLogTable';
import { GitWorkingTreeInspector } from '../src/components/workspaces/studio/git/GitWorkingTreeInspector';
import { StudioHeader } from '../src/components/workspaces/studio/StudioHeader';
import { GitCommitDto, GitStatusDto, GitBranchDto } from '../src/types/git';
import { WorkspaceDto } from '../src/types/workspace';
import { ToastProvider } from '../src/context/ToastContext';

describe('Git Workbench UI Components', () => {
  describe('GitCommitGraphCell', () => {
    it('renders dashed marker circle for working tree row', () => {
      const row = {
        id: 'working-tree',
        lane: 0,
        color: '#22c55e',
        maxLane: 0,
        upLinks: [],
        downLinks: [],
        passLanes: [],
        isWorkingTree: true,
      };

      const html = renderToString(<GitCommitGraphCell row={row} />);
      expect(html).toContain('git-graph-svg');
      expect(html).toContain('stroke-dasharray="2 2"');
      expect(html).toContain('stroke="#22c55e"');
    });

    it('renders normal circle and links for commit row', () => {
      const row = {
        id: 'commit123',
        lane: 1,
        color: '#4ade80',
        maxLane: 1,
        upLinks: [{ fromLane: 1, toLane: 1, color: '#4ade80' }],
        downLinks: [{ fromLane: 1, toLane: 0, color: '#38bdf8' }],
        passLanes: [{ lane: 0, color: '#38bdf8' }],
        isWorkingTree: false,
      };

      const html = renderToString(<GitCommitGraphCell row={row} />);
      expect(html).toContain('git-graph-svg');
      expect(html).not.toContain('stroke-dasharray="2 2"');
      expect(html).toContain('fill="#4ade80"');
    });
  });

  describe('GitCommitLogTable', () => {
    const mockStatus: GitStatusDto = {
      isGitInstalled: true,
      isRepository: true,
      currentBranch: 'feature/git-workbench',
      ahead: 2,
      behind: 1,
      staged: [{ path: 'src/file1.ts', status: 'M', isStaged: true }],
      modified: [{ path: 'src/file2.ts', status: 'M', isStaged: false }],
      untracked: ['src/newfile.ts'],
    };

    const mockCommits: GitCommitDto[] = [
      {
        hash: 'a1b2c3d4e5f6',
        shortHash: 'a1b2c3d',
        author: 'Developer One',
        email: 'dev1@example.com',
        date: '2026-10-18T12:00:00Z',
        message: 'feat: add git workbench log table',
        parents: ['f6e5d4c3b2a1'],
        refs: 'HEAD -> feature/git-workbench, origin/feature/git-workbench',
      },
    ];

    const mockBranches: GitBranchDto[] = [
      { name: 'feature/git-workbench', isCurrent: true, isRemote: false },
      { name: 'main', isCurrent: false, isRemote: false },
    ];

    it('renders Row 0 with WORKING TREE CHANGES and uncommitted change stats', () => {
      const html = renderToString(
        <GitCommitLogTable
          commits={mockCommits}
          status={mockStatus}
          selectedRowId="working-tree"
          onSelectRow={vi.fn()}
          branches={mockBranches}
          onCheckoutBranch={vi.fn()}
          onCreateBranch={vi.fn()}
          onFetch={vi.fn()}
          onPull={vi.fn()}
          onPush={vi.fn()}
          onRefresh={vi.fn()}
        />
      );

      // Row 0 Working Tree verification
      expect(html).toContain('id="row-working-tree"');
      expect(html).toContain('Working Tree Changes');
      expect(html).toContain('UNCOMMITTED');
      expect(html).toContain('staged');
      expect(html).toContain('modified');
      expect(html).toContain('untracked');

      // Commit Row verification
      expect(html).toContain('commit-row-a1b2c3d');
      expect(html).toContain('a1b2c3d');
      expect(html).toContain('feat: add git workbench log table');
      expect(html).toContain('Developer One');

      // Ref Badges
      expect(html).toContain('HEAD -&gt; feature/git-workbench');
      expect(html).toContain('origin/feature/git-workbench');

      // Ahead / Behind indicators in toolbar
      expect(html).toContain('↑');
      expect(html).toContain('↓');
    });
  });

  describe('GitWorkingTreeInspector', () => {
    it('renders staged, modified, and untracked groups with Diff buttons', () => {
      const mockStatus: GitStatusDto = {
        isGitInstalled: true,
        isRepository: true,
        currentBranch: 'main',
        ahead: 0,
        behind: 0,
        staged: [{ path: 'staged-file.ts', status: 'M', isStaged: true }],
        modified: [{ path: 'mod-file.ts', status: 'M', isStaged: false }],
        untracked: ['untracked-file.ts'],
      };

      const html = renderToString(
        <GitWorkingTreeInspector
          status={mockStatus}
          commitMessage="test commit"
          onCommitMessageChange={vi.fn()}
          onCommit={vi.fn()}
          onStage={vi.fn()}
          onUnstage={vi.fn()}
          onViewDiff={vi.fn()}
          onStash={vi.fn()}
        />
      );

      expect(html).toContain('Staged Changes');
      expect(html).toContain('staged-file.ts');
      expect(html).toContain('Modified / Changes');
      expect(html).toContain('mod-file.ts');
      expect(html).toContain('Untracked Files');
      expect(html).toContain('untracked-file.ts');

      // Diff buttons must be rendered for staged and modified files
      expect(html).toContain('Diff');

      // Commit message input & submit button
      expect(html).toContain('id="workbenchCommitMessageInput"');
      expect(html).toContain('Commit (1 staged)');
    });

    it('renders clean state message when no uncommitted changes', () => {
      const cleanStatus: GitStatusDto = {
        isGitInstalled: true,
        isRepository: true,
        currentBranch: 'main',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(
        <GitWorkingTreeInspector
          status={cleanStatus}
          commitMessage=""
          onCommitMessageChange={vi.fn()}
          onCommit={vi.fn()}
          onStage={vi.fn()}
          onUnstage={vi.fn()}
          onViewDiff={vi.fn()}
          onStash={vi.fn()}
        />
      );

      expect(html).toContain('Working directory is clean.');
      expect(html).toContain('No uncommitted changes detected');
    });
  });

  describe('StudioHeader Mode Switcher', () => {
    const mockWs: WorkspaceDto = {
      id: 'ws-123',
      name: 'Test Project',
      path: '/path/to/project',
      createdAtUtc: '2026-10-18T00:00:00Z',
      updatedAtUtc: '2026-10-18T00:00:00Z',
      conversationCount: 1,
    };

    it('renders segmented mode switch buttons with active state', () => {
      const handleSwitch = vi.fn();
      const html = renderToString(
        <ToastProvider>
          <StudioHeader
            workspace={mockWs}
            activeConversation={null}
            models={[]}
            showActionsMenu={false}
            studioMode="git"
            onSwitchStudioMode={handleSwitch}
            onBack={vi.fn()}
            onModelChange={vi.fn()}
            onToggleActionsMenu={vi.fn()}
            onCloseActionsMenu={vi.fn()}
            onNewConversation={vi.fn()}
            onOpenDiffs={vi.fn()}
            onDownloadZip={vi.fn()}
            onEffortChange={vi.fn()}
            onDeleteConversation={vi.fn()}
          />
        </ToastProvider>
      );

      expect(html).toContain('id="studioModeChatBtn"');
      expect(html).toContain('id="studioModeGitBtn"');
      expect(html).toContain('Git Workbench');
      // When studioMode is 'git', Git button should have btn-primary
      expect(html).toMatch(/class="[^"]*btn compact-btn btn-primary[^"]*"[^>]*id="studioModeGitBtn"/);
    });
  });
});
