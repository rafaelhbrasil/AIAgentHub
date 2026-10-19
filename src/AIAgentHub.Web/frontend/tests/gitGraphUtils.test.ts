import { describe, it, expect } from 'vitest';
import { computeCommitGraph } from '../src/components/workspaces/studio/git/gitGraphUtils';
import { GitCommitDto } from '../src/types/git';

describe('gitGraphUtils - computeCommitGraph', () => {
  it('handles empty commit history with working tree included', () => {
    const rows = computeCommitGraph([], true);
    expect(rows).toHaveLength(1);
    expect(rows[0].id).toBe('working-tree');
    expect(rows[0].lane).toBe(0);
    expect(rows[0].isWorkingTree).toBe(true);
    expect(rows[0].downLinks).toHaveLength(0);
  });

  it('allocates linear single lane for straight commits', () => {
    const commits: GitCommitDto[] = [
      {
        hash: 'commit3',
        shortHash: 'c3',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T10:00:00Z',
        message: 'feat: three',
        parents: ['commit2'],
      },
      {
        hash: 'commit2',
        shortHash: 'c2',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T09:00:00Z',
        message: 'feat: two',
        parents: ['commit1'],
      },
      {
        hash: 'commit1',
        shortHash: 'c1',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T08:00:00Z',
        message: 'initial commit',
        parents: [],
      },
    ];

    const rows = computeCommitGraph(commits, true);
    // 1 working tree + 3 commits = 4 rows
    expect(rows).toHaveLength(4);

    // Row 0: working-tree
    expect(rows[0].id).toBe('working-tree');
    expect(rows[0].lane).toBe(0);
    expect(rows[0].downLinks).toEqual([{ fromLane: 0, toLane: 0, color: '#22c55e' }]);

    // Row 1: commit3
    expect(rows[1].id).toBe('commit3');
    expect(rows[1].lane).toBe(0);
    expect(rows[1].downLinks).toHaveLength(1);
    expect(rows[1].downLinks[0].fromLane).toBe(0);
    expect(rows[1].downLinks[0].toLane).toBe(0);

    // Row 3: root commit (commit1) has 0 downLinks
    expect(rows[3].id).toBe('commit1');
    expect(rows[3].lane).toBe(0);
    expect(rows[3].downLinks).toHaveLength(0);
  });

  it('allocates fork/merge lanes for merge commits', () => {
    const commits: GitCommitDto[] = [
      {
        hash: 'mergeCommit',
        shortHash: 'm1',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T11:00:00Z',
        message: 'Merge branch feature',
        parents: ['mainCommit', 'featureCommit'],
      },
      {
        hash: 'mainCommit',
        shortHash: 'mc1',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T10:00:00Z',
        message: 'feat: main branch change',
        parents: ['rootCommit'],
      },
      {
        hash: 'featureCommit',
        shortHash: 'fc1',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T09:30:00Z',
        message: 'feat: feature branch change',
        parents: ['rootCommit'],
      },
      {
        hash: 'rootCommit',
        shortHash: 'r1',
        author: 'Dev',
        email: 'dev@test.com',
        date: '2026-10-18T08:00:00Z',
        message: 'initial',
        parents: [],
      },
    ];

    const rows = computeCommitGraph(commits, false);
    expect(rows).toHaveLength(4);

    // mergeCommit should have 2 downLinks (one for mainCommit, one for featureCommit)
    const mergeRow = rows[0];
    expect(mergeRow.id).toBe('mergeCommit');
    expect(mergeRow.downLinks).toHaveLength(2);
    expect(mergeRow.downLinks[0].toLane).toBe(0);
    expect(mergeRow.downLinks[1].toLane).toBe(1);

    // featureCommit should be on lane 1
    const featureRow = rows[2];
    expect(featureRow.id).toBe('featureCommit');
    expect(featureRow.lane).toBe(1);

    // maxLane should be >= 1 across all rows
    expect(mergeRow.maxLane).toBeGreaterThanOrEqual(1);
    expect(rows[3].maxLane).toBeGreaterThanOrEqual(1);
  });
});
