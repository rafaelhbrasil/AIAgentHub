import { describe, it, expect } from 'vitest';
import { FileTreeNode } from '../src/types/workspace';

function filterTree(node: FileTreeNode, query: string): FileTreeNode | null {
  if (!query) return node;
  const lowerQuery = query.toLowerCase();
  const nameMatches =
    node.name.toLowerCase().includes(lowerQuery) ||
    (node.relativePath && node.relativePath.toLowerCase().includes(lowerQuery));

  if (!node.isDirectory) {
    return nameMatches ? node : null;
  }

  const filteredChildren: FileTreeNode[] = [];
  if (node.children) {
    for (const child of node.children) {
      const filtered = filterTree(child, query);
      if (filtered) {
        filteredChildren.push(filtered);
      }
    }
  }

  if (nameMatches || filteredChildren.length > 0) {
    return {
      ...node,
      children: filteredChildren,
    };
  }

  return null;
}

describe('FileTree search filter logic', () => {
  const sampleTree: FileTreeNode = {
    name: 'project-root',
    relativePath: '',
    isDirectory: true,
    children: [
      { name: 'package.json', relativePath: 'package.json', isDirectory: false },
      { name: 'README.md', relativePath: 'README.md', isDirectory: false },
      {
        name: 'src',
        relativePath: 'src',
        isDirectory: true,
        children: [
          { name: 'index.ts', relativePath: 'src/index.ts', isDirectory: false },
          { name: 'App.tsx', relativePath: 'src/App.tsx', isDirectory: false },
          {
            name: 'components',
            relativePath: 'src/components',
            isDirectory: true,
            children: [
              { name: 'Header.tsx', relativePath: 'src/components/Header.tsx', isDirectory: false },
              { name: 'FileTree.tsx', relativePath: 'src/components/FileTree.tsx', isDirectory: false },
            ],
          },
        ],
      },
      {
        name: 'docs',
        relativePath: 'docs',
        isDirectory: true,
        children: [
          { name: 'Architecture.md', relativePath: 'docs/Architecture.md', isDirectory: false },
        ],
      },
    ],
  };

  it('returns whole tree unmodified when query is empty', () => {
    const result = filterTree(sampleTree, '');
    expect(result).toEqual(sampleTree);
  });

  it('filters tree to matching leaf file and preserves ancestor directories', () => {
    const result = filterTree(sampleTree, 'FileTree');
    expect(result).not.toBeNull();
    expect(result?.children).toHaveLength(1);
    expect(result?.children?.[0].name).toBe('src');

    const srcNode = result?.children?.[0];
    expect(srcNode?.children).toHaveLength(1);
    expect(srcNode?.children?.[0].name).toBe('components');

    const compNode = srcNode?.children?.[0];
    expect(compNode?.children).toHaveLength(1);
    expect(compNode?.children?.[0].name).toBe('FileTree.tsx');
  });

  it('matches all markdown files across different directories', () => {
    const result = filterTree(sampleTree, '.md');
    expect(result).not.toBeNull();
    // README.md in root and docs/Architecture.md
    const childNames = result?.children?.map((c) => c.name);
    expect(childNames).toContain('README.md');
    expect(childNames).toContain('docs');
    expect(childNames).not.toContain('package.json');
  });

  it('returns null when no file or folder matches the query', () => {
    const result = filterTree(sampleTree, 'nonexistent_pattern_xyz');
    expect(result).toBeNull();
  });
});
