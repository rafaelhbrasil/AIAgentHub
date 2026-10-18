import { describe, it, expect } from 'vitest';

describe('Code Editor & Workspace Clone helpers', () => {
  describe('extractRepoName helper', () => {
    const extractRepoName = (url: string): string => {
      if (!url) return '';
      const clean = url.trim().replace(/\.git$/i, '').replace(/[/\\]+$/, '');
      const parts = clean.split(/[/:/\\]/);
      return parts[parts.length - 1] || '';
    };

    it('extracts repository name from HTTPS GitHub URLs', () => {
      expect(extractRepoName('https://github.com/owner/my-cool-project.git')).toBe('my-cool-project');
      expect(extractRepoName('https://github.com/owner/my-cool-project')).toBe('my-cool-project');
    });

    it('extracts repository name from SSH Git URLs', () => {
      expect(extractRepoName('git@github.com:owner/custom-repo.git')).toBe('custom-repo');
      expect(extractRepoName('git@gitlab.com:team/subgroup/deep-repo.git')).toBe('deep-repo');
    });

    it('handles trailing slashes and spaces cleanly', () => {
      expect(extractRepoName('  https://github.com/org/repo/  ')).toBe('repo');
    });

    it('returns empty string for empty input', () => {
      expect(extractRepoName('')).toBe('');
      expect(extractRepoName('   ')).toBe('');
    });
  });

  describe('File path extraction for quick switcher (Ctrl+P)', () => {
    interface FileNode {
      name: string;
      relativePath: string;
      isDirectory: boolean;
      children?: FileNode[];
    }

    const extractFilePaths = (node: FileNode | null): string[] => {
      if (!node) return [];
      const results: string[] = [];
      const traverse = (current: FileNode) => {
        if (!current.isDirectory && current.relativePath) {
          results.push(current.relativePath);
        }
        if (current.children) {
          for (const child of current.children) {
            traverse(child);
          }
        }
      };
      traverse(node);
      return results;
    };

    it('extracts all recursive file paths from a file tree', () => {
      const tree: FileNode = {
        name: 'root',
        relativePath: '',
        isDirectory: true,
        children: [
          { name: 'README.md', relativePath: 'README.md', isDirectory: false },
          {
            name: 'src',
            relativePath: 'src',
            isDirectory: true,
            children: [
              { name: 'index.ts', relativePath: 'src/index.ts', isDirectory: false },
              {
                name: 'utils',
                relativePath: 'src/utils',
                isDirectory: true,
                children: [
                  { name: 'math.ts', relativePath: 'src/utils/math.ts', isDirectory: false },
                ],
              },
            ],
          },
        ],
      };

      const paths = extractFilePaths(tree);
      expect(paths).toEqual(['README.md', 'src/index.ts', 'src/utils/math.ts']);
    });

    it('returns empty array when tree is null or contains only directories', () => {
      expect(extractFilePaths(null)).toEqual([]);
      expect(extractFilePaths({ name: 'empty', relativePath: '', isDirectory: true, children: [] })).toEqual([]);
    });
  });
});
