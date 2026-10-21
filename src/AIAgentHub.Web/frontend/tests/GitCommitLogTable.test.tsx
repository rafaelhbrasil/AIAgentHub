import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { GitCommitLogTable } from '../src/components/workspaces/studio/git/GitCommitLogTable';
import { GitCommitDto, GitStatusDto, GitBranchDto } from '../src/types/git';

describe('GitCommitLogTable', () => {
  let container: HTMLDivElement;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  const sampleCommits: GitCommitDto[] = [
    {
      hash: 'commit-1111111111111111111',
      shortHash: 'c111111',
      author: 'Alice Engineer',
      email: 'alice@example.com',
      date: '2026-10-18T12:00:00Z',
      message: 'feat: implement shiny feature',
      parents: [],
      refs: 'HEAD -> main, tag: v1.0.0, origin/main',
    },
    {
      hash: 'commit-2222222222222222222',
      shortHash: 'c222222',
      author: 'Bob Developer',
      email: 'bob@example.com',
      date: 'invalid-date-format',
      message: 'fix: resolve edge-case in splitter',
      parents: ['commit-1111111111111111111'],
      refs: 'feature/splitter',
    },
  ];

  const sampleStatus: GitStatusDto = {
    isGitInstalled: true,
    isRepository: true,
    currentBranch: 'custom-detached-branch',
    ahead: 2,
    behind: 1,
    staged: [{ path: 'src/staged.ts', status: 'M', isStaged: true }],
    modified: [{ path: 'src/mod.ts', status: 'M', isStaged: false }],
    untracked: ['src/untracked.ts'],
  };

  const sampleBranches: GitBranchDto[] = [
    { name: 'main', isCurrent: false, isRemote: false },
    { name: 'develop', isCurrent: false, isRemote: false },
  ];

  it('renders commit log table with working tree, commits, badges, and current branch fallback', async () => {
    const onSelectRow = vi.fn();
    const onCheckoutBranch = vi.fn();
    const onCreateBranch = vi.fn();
    const onFetch = vi.fn();
    const onPull = vi.fn();
    const onPush = vi.fn();
    const onRefresh = vi.fn();

    const root = createRoot(container);
    await act(async () => {
      root.render(
        <GitCommitLogTable
          commits={sampleCommits}
          status={sampleStatus}
          selectedRowId="working-tree"
          onSelectRow={onSelectRow}
          branches={sampleBranches}
          onCheckoutBranch={onCheckoutBranch}
          onCreateBranch={onCreateBranch}
          onFetch={onFetch}
          onPull={onPull}
          onPush={onPush}
          onRefresh={onRefresh}
        />
      );
    });

    expect(container.textContent).toContain('Working Tree');
    expect(container.textContent).toContain('custom-detached-branch (current)');
    expect(container.textContent).toContain('tag: v1.0.0');
    expect(container.textContent).toContain('HEAD -> main');
    expect(container.textContent).toContain('origin/main');
    expect(container.textContent).toContain('Alice Engineer');
    expect(container.textContent).toContain('Bob Developer');

    // 1. Select commit row
    const row2 = Array.from(container.querySelectorAll('.git-log-row')).find((r) =>
      r.textContent?.includes('Bob Developer')
    );
    expect(row2).not.toBeUndefined();
    await act(async () => {
      (row2 as HTMLElement).click();
    });
    expect(onSelectRow).toHaveBeenCalledWith('commit-2222222222222222222');

    // 2. Filter commits by search input
    const filterInput = container.querySelector('#workbenchCommitFilter') as HTMLInputElement;
    expect(filterInput).not.toBeNull();
    const setInputValue = async (val: string) => {
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(filterInput, val);
        filterInput.dispatchEvent(new Event('input', { bubbles: true }));
        filterInput.dispatchEvent(new Event('change', { bubbles: true }));
      });
    };

    if (filterInput) {
      // Filter by shortHash
      await setInputValue('c111111');
      expect(container.textContent).toContain('feat: implement shiny feature');
      expect(container.textContent).not.toContain('Bob Developer');

      // Filter by tag
      await setInputValue('v1.0.0');
      expect(container.textContent).toContain('feat: implement shiny feature');

      // Filter matching nothing
      await setInputValue('nomatch_query_xyz');
      expect(container.textContent).toContain('No commits match the filter query.');
    }

    // 3. Toolbar button actions
    const newBtn = container.querySelector('#workbenchNewBranchBtn') as HTMLButtonElement;
    await act(async () => {
      newBtn?.click();
    });
    expect(onCreateBranch).toHaveBeenCalled();

    const fetchBtn = container.querySelector('#workbenchFetchBtn') as HTMLButtonElement;
    await act(async () => {
      fetchBtn?.click();
    });
    expect(onFetch).toHaveBeenCalled();

    const pullBtn = container.querySelector('#workbenchPullBtn') as HTMLButtonElement;
    await act(async () => {
      pullBtn?.click();
    });
    expect(onPull).toHaveBeenCalled();

    const pushBtn = container.querySelector('#workbenchPushBtn') as HTMLButtonElement;
    await act(async () => {
      pushBtn?.click();
    });
    expect(onPush).toHaveBeenCalled();

    const refreshBtn = container.querySelector('#workbenchRefreshBtn') as HTMLButtonElement;
    await act(async () => {
      refreshBtn?.click();
    });
    expect(onRefresh).toHaveBeenCalled();

    // 4. Branch select change
    const branchSelect = container.querySelector('#workbenchBranchSelect') as HTMLSelectElement;
    await act(async () => {
      branchSelect.value = 'main';
      branchSelect.dispatchEvent(new Event('change', { bubbles: true }));
    });
    expect(onCheckoutBranch).toHaveBeenCalledWith('main');

    act(() => {
      root.unmount();
    });
    container.remove();
  });

  it('renders empty commit message when commits array is empty', async () => {
    const root = createRoot(container);
    await act(async () => {
      root.render(
        <GitCommitLogTable
          commits={[]}
          status={null}
          selectedRowId="working-tree"
          onSelectRow={vi.fn()}
          branches={[]}
          onCheckoutBranch={vi.fn()}
          onCreateBranch={vi.fn()}
          onFetch={vi.fn()}
          onPull={vi.fn()}
          onPush={vi.fn()}
          onRefresh={vi.fn()}
        />
      );
    });

    expect(container.textContent).toContain('No commit history found in this repository.');

    act(() => {
      root.unmount();
    });
    container.remove();
  });
});
