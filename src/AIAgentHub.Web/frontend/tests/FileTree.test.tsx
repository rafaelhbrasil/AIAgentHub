import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { FileTree } from '../src/components/workspaces/FileTree';
import { FileTreeNode } from '../src/types/workspace';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';

describe('FileTree UI Component', () => {
  const sampleTree: FileTreeNode = {
    name: 'root',
    relativePath: '',
    isDirectory: true,
    children: [
      { name: 'README.md', relativePath: 'README.md', isDirectory: false },
      { name: 'logo.png', relativePath: 'logo.png', isDirectory: false },
      {
        name: 'src',
        relativePath: 'src',
        isDirectory: true,
        children: [
          { name: 'index.ts', relativePath: 'src/index.ts', isDirectory: false },
          { name: 'app.css', relativePath: 'src/app.css', isDirectory: false },
          { name: 'data.json', relativePath: 'src/data.json', isDirectory: false },
        ],
      },
    ],
  };

  it('renders empty folder message when node is null', () => {
    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <FileTree
            workspaceId="ws-123"
            node={null}
            onSelectFile={vi.fn()}
          />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('Empty folder');
  });

  it('renders root folder, search filter, and child files when tree is provided', () => {
    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <FileTree
            workspaceId="ws-123"
            node={sampleTree}
            onSelectFile={vi.fn()}
            onOpenEditor={vi.fn()}
            onRefresh={vi.fn()}
          />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('fileTreeFilterInput');
    expect(html).toContain('workspaceTree');
    expect(html).toContain('README.md');
    expect(html).toContain('logo.png');
    expect(html).toContain('src');
    expect(html).toContain('treeMenuBtn-root');
    expect(html).toContain('treeMenuBtn-src');
  });

  it('renders non-root leaf file with correct icon and attributes', () => {
    const singleFileTree: FileTreeNode = {
      name: 'SingleFile',
      relativePath: 'SingleFile.cs',
      isDirectory: false,
    };

    const html = renderToString(
      <ToastProvider>
        <ModalProvider>
          <FileTree
            workspaceId="ws-123"
            node={singleFileTree}
            onSelectFile={vi.fn()}
          />
        </ModalProvider>
      </ToastProvider>
    );

    expect(html).toContain('SingleFile');
    expect(html).toContain('file-tree-node');
  });
});
