import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { GitCommitDetailInspector } from '../src/components/workspaces/studio/git/GitCommitDetailInspector';
import { GitCommitDetailDto } from '../src/types/git';

describe('GitCommitDetailInspector', () => {
  const mockDetail: GitCommitDetailDto = {
    hash: 'a1b2c3d4e5f67890abcdef1234567890abcdef12',
    shortHash: 'a1b2c3d',
    author: 'Alice Developer',
    email: 'alice@example.com',
    date: '2026-10-18T14:30:00Z',
    message: 'feat(git): add commit details inspector\n\nDetailed multiline explanation.',
    parents: ['1234567890abcdef', 'abcdef1234567890'],
    refs: 'HEAD -> feature/git-studio, origin/feature/git-studio',
    files: [
      { path: 'src/components/FileA.tsx', status: 'Modified', additions: 12, deletions: 4 },
      { path: 'src/assets/logo.png', status: 'Added', additions: 0, deletions: 0 },
      { path: 'old/deleted.ts', status: 'Deleted', additions: 0, deletions: 50 },
      { path: 'src/renamed.ts', status: 'Renamed', additions: 5, deletions: 2 },
    ],
  };

  it('renders commit metadata, parents, refs, and stats correctly', () => {
    const handleSelectFile = vi.fn();
    const handleViewDiff = vi.fn();

    const html = renderToString(
      <GitCommitDetailInspector
        workspaceId="ws-123"
        commitHash={mockDetail.hash}
        initialDetail={mockDetail}
        onViewDiff={handleViewDiff}
        onFileSelect={handleSelectFile}
      />
    );

    // Commit headers
    expect(html).toContain('a1b2c3d');
    expect(html).toContain(mockDetail.hash);
    expect(html).toContain('Alice Developer');
    expect(html).toContain('alice@example.com');
    expect(html).toContain('feat(git): add commit details inspector');
    expect(html).toContain('Detailed multiline explanation.');

    // Changed files & badges
    expect(html).toContain('Changed Files');
    expect(html).toContain('src/components/FileA.tsx');
    expect(html).toContain('12');
    expect(html).toContain('4');
    expect(html).toContain('src/assets/logo.png');
    expect(html).toContain('old/deleted.ts');
    expect(html).toContain('src/renamed.ts');

    // Status badges
    expect(html).toContain('badge');
  });

  it('renders clean fallback when detail has no files', () => {
    const emptyDetail: GitCommitDetailDto = {
      hash: 'root1234',
      shortHash: 'root123',
      author: 'Bob',
      email: 'bob@example.com',
      date: '2026-10-18T00:00:00Z',
      message: 'initial commit',
      parents: [],
      refs: null,
      files: [],
    };

    const html = renderToString(
      <GitCommitDetailInspector
        workspaceId="ws-123"
        commitHash={emptyDetail.hash}
        initialDetail={emptyDetail}
        onViewDiff={vi.fn()}
      />
    );

    expect(html).toContain('root123');
    expect(html).toContain('initial commit');
    expect(html).toContain('No file modifications in this commit.');
  });
});
