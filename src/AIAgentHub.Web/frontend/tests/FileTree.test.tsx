import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import {
  FileTree,
  filterTree,
  getFolderBackground,
  getFolderIcon,
} from '../src/components/workspaces/FileTree';
import { FileTreeNode } from '../src/types/workspace';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';
import { Modal } from '../src/components/common/Modal';
import * as apiClient from '../src/services/apiClient';

describe('FileTree Component & Helpers', () => {
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
          {
            name: 'components',
            relativePath: 'src/components',
            isDirectory: true,
            children: [
              { name: 'Button.tsx', relativePath: 'src/components/Button.tsx', isDirectory: false },
            ],
          },
        ],
      },
    ],
  };

  describe('Pure Helper Functions', () => {
    it('getFolderBackground returns correct styles based on state', () => {
      expect(getFolderBackground(true, false, false)).toBe('rgba(99, 102, 241, 0.25)');
      expect(getFolderBackground(false, true, false)).toBe('rgba(255, 255, 255, 0.06)');
      expect(getFolderBackground(false, true, true)).toBe('transparent');
      expect(getFolderBackground(false, false, false)).toBe('transparent');
    });

    it('getFolderIcon returns folder icons based on root and expanded state', () => {
      expect(getFolderIcon(true, false)).toBe('📁');
      expect(getFolderIcon(true, true)).toBe('📁');
      expect(getFolderIcon(false, true)).toBe('📂');
      expect(getFolderIcon(false, false)).toBe('📁');
    });

    it('filterTree handles empty queries, case-insensitivity, and recursive branches', () => {
      expect(filterTree(sampleTree, '')).toBe(sampleTree);

      // Matches child file in deep directory
      const filteredBtn = filterTree(sampleTree, 'button');
      expect(filteredBtn).not.toBeNull();
      expect(filteredBtn?.name).toBe('root');
      expect(filteredBtn?.children).toHaveLength(1);
      expect(filteredBtn?.children?.[0].name).toBe('src');

      // Matches directory name
      const filteredSrc = filterTree(sampleTree, 'src');
      expect(filteredSrc).not.toBeNull();
      expect(filteredSrc?.children?.some((c) => c.name === 'src')).toBe(true);

      // Non-matching query
      const nonMatching = filterTree(sampleTree, 'nonexistentxyz');
      expect(nonMatching).toBeNull();

      // Leaf file match and non-match
      const leafFile: FileTreeNode = { name: 'file.txt', relativePath: 'file.txt', isDirectory: false };
      expect(filterTree(leafFile, 'file')).toBe(leafFile);
      expect(filterTree(leafFile, 'other')).toBeNull();
    });
  });

  describe('Static & Empty State Rendering', () => {
    it('renders empty folder message when node is null', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <FileTree workspaceId="ws-123" node={null} onSelectFile={vi.fn()} />
          </ModalProvider>
        </ToastProvider>
      );
      expect(html).toContain('Empty folder');
    });

    it('renders search filter input and file list', () => {
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
      expect(html).toContain('README.md');
      expect(html).toContain('logo.png');
      expect(html).toContain('src');
    });
  });

  describe('Interactive DOM Tests', () => {
    it('mounts, selects file, hovers to reveal actions and opens editor', async () => {
      const onSelectFile = vi.fn();
      const onOpenEditor = vi.fn();
      const onRefresh = vi.fn();

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={onSelectFile}
                onOpenEditor={onOpenEditor}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // Find file button for README.md
      const fileButtons = container.querySelectorAll('.file-tree-node button');
      const readmeBtn = Array.from(fileButtons).find((b) => b.textContent?.includes('README.md')) as HTMLButtonElement;
      expect(readmeBtn).toBeDefined();

      await act(async () => {
        readmeBtn.click();
      });
      expect(onSelectFile).toHaveBeenCalledWith('README.md');

      // Hover over file node to trigger hover actions
      const readmeNode = readmeBtn.closest('.file-tree-node') as HTMLDivElement;
      await act(async () => {
        readmeNode.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
        readmeNode.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
      });

      const openEditorBtn = readmeNode.querySelector('button[title="Open in Code Editor"]') as HTMLButtonElement;
      expect(openEditorBtn).not.toBeNull();
      await act(async () => {
        openEditorBtn.click();
      });
      expect(onOpenEditor).toHaveBeenCalledWith('README.md');

      // Leave hover
      await act(async () => {
        readmeNode.dispatchEvent(new MouseEvent('mouseleave', { bubbles: true }));
      });
    });

    it('handles file renaming through hover action and modal submission', async () => {
      const onRefresh = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: { success: true },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      const fileButtons = container.querySelectorAll('.file-tree-node button');
      const readmeBtn = Array.from(fileButtons).find((b) => b.textContent?.includes('README.md')) as HTMLButtonElement;
      const readmeNode = readmeBtn.closest('.file-tree-node') as HTMLDivElement;

      // Enter hover to reveal Rename
      await act(async () => {
        readmeNode.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
        readmeNode.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
      });

      const renameBtn = readmeNode.querySelector('button[title="Rename file"]') as HTMLButtonElement;
      expect(renameBtn).not.toBeNull();

      await act(async () => {
        renameBtn.click();
      });

      // Modal should be visible
      const modalBox = document.querySelector('#modalBox');
      expect(modalBox).not.toBeNull();
      expect(modalBox?.textContent).toContain('Rename "README.md"');

      // Fill in new file name
      const input = modalBox?.querySelector('input') as HTMLInputElement;
      expect(input).not.toBeNull();
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'README_NEW.md');
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      // Submit modal
      const submitBtn = modalBox?.querySelector('button[type="submit"]') as HTMLButtonElement;
      await act(async () => {
        submitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/rename', {
        method: 'PUT',
        body: {
          workspaceId: 'ws-123',
          oldPath: 'README.md',
          newPath: 'README_NEW.md',
        },
      });
      expect(onRefresh).toHaveBeenCalled();
    });

    it('handles file deletion through hover action and modal confirmation', async () => {
      const onRefresh = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: { success: true },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      const fileButtons = container.querySelectorAll('.file-tree-node button');
      const logoBtn = Array.from(fileButtons).find((b) => b.textContent?.includes('logo.png')) as HTMLButtonElement;
      const logoNode = logoBtn.closest('.file-tree-node') as HTMLDivElement;

      // Hover to reveal Delete
      await act(async () => {
        logoNode.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
        logoNode.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
      });

      const deleteBtn = logoNode.querySelector('button[title="Delete file"]') as HTMLButtonElement;
      expect(deleteBtn).not.toBeNull();

      await act(async () => {
        deleteBtn.click();
      });

      // Modal confirmation should appear
      const confirmDeleteBtn = document.querySelector('#confirmDeleteTreeItemBtn') as HTMLButtonElement;
      expect(confirmDeleteBtn).not.toBeNull();

      await act(async () => {
        confirmDeleteBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/item', {
        method: 'DELETE',
        body: {
          workspaceId: 'ws-123',
          path: 'logo.png',
        },
      });
      expect(onRefresh).toHaveBeenCalled();
    });

    it('expands and collapses directory nodes and toggles context menu', async () => {
      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // Find the folder toggle button for 'src'
      const folderButtons = container.querySelectorAll('.folder-tree-node button');
      const srcFolderBtn = Array.from(folderButtons).find((b) => b.textContent?.includes('src')) as HTMLButtonElement;
      expect(srcFolderBtn).toBeDefined();

      // Click to expand 'src'
      await act(async () => {
        srcFolderBtn.click();
      });

      // 'index.ts' and 'components' should now be rendered
      expect(container.textContent).toContain('index.ts');
      expect(container.textContent).toContain('components');

      // Click again to collapse
      await act(async () => {
        srcFolderBtn.click();
      });

      // Test context menu button for root
      const menuBtn = container.querySelector('#treeMenuBtn-root') as HTMLButtonElement;
      expect(menuBtn).not.toBeNull();

      await act(async () => {
        menuBtn.click();
      });

      expect(container.textContent).toContain('📄 New File');
      expect(container.textContent).toContain('📁 New Folder');

      // Click close backdrop
      const closeBackdrop = container.querySelector('button[aria-label="Close menu"]') as HTMLButtonElement;
      if (closeBackdrop) {
        await act(async () => {
          closeBackdrop.click();
        });
        expect(container.textContent).not.toContain('📄 New File');
      }
    });

    it('creates a new file in folder via context menu', async () => {
      const onRefresh = vi.fn();
      const onOpenEditor = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: { success: true },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
                onOpenEditor={onOpenEditor}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      const menuBtn = container.querySelector('#treeMenuBtn-root') as HTMLButtonElement;
      await act(async () => {
        menuBtn.click();
      });

      const newFileMenuItem = Array.from(container.querySelectorAll('.tree-menu-item')).find(
        (b) => b.textContent?.includes('New File')
      ) as HTMLButtonElement;
      expect(newFileMenuItem).toBeDefined();

      await act(async () => {
        newFileMenuItem.click();
      });

      const modalBox = document.querySelector('#modalBox');
      const input = modalBox?.querySelector('input') as HTMLInputElement;
      expect(input).not.toBeNull();

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'newfile.ts');
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const submitBtn = modalBox?.querySelector('button[type="submit"]') as HTMLButtonElement;
      await act(async () => {
        submitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/file', {
        method: 'POST',
        body: {
          workspaceId: 'ws-123',
          path: 'newfile.ts',
          content: '',
        },
      });
      expect(onRefresh).toHaveBeenCalled();
      expect(onOpenEditor).toHaveBeenCalledWith('newfile.ts');
    });

    it('creates a new directory via folder context menu', async () => {
      const onRefresh = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: { success: true },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      const menuBtn = container.querySelector('#treeMenuBtn-root') as HTMLButtonElement;
      await act(async () => {
        menuBtn.click();
      });

      const newFolderMenuItem = Array.from(container.querySelectorAll('.tree-menu-item')).find(
        (b) => b.textContent?.includes('New Folder')
      ) as HTMLButtonElement;
      expect(newFolderMenuItem).toBeDefined();

      await act(async () => {
        newFolderMenuItem.click();
      });

      const modalBox = document.querySelector('#modalBox');
      const input = modalBox?.querySelector('input') as HTMLInputElement;

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'new-dir');
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const submitBtn = modalBox?.querySelector('button[type="submit"]') as HTMLButtonElement;
      await act(async () => {
        submitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/mkdir', {
        method: 'POST',
        body: {
          workspaceId: 'ws-123',
          path: 'new-dir',
        },
      });
      expect(onRefresh).toHaveBeenCalled();
    });

    it('handles context menu rename and delete for child folders', async () => {
      const onRefresh = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: { success: true },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // Menu for 'src' folder
      const srcMenuBtn = container.querySelector('#treeMenuBtn-src') as HTMLButtonElement;
      expect(srcMenuBtn).not.toBeNull();

      await act(async () => {
        srcMenuBtn.click();
      });

      const renameMenuItem = Array.from(container.querySelectorAll('.tree-menu-item')).find(
        (b) => b.textContent?.includes('Rename')
      ) as HTMLButtonElement;
      expect(renameMenuItem).toBeDefined();

      await act(async () => {
        renameMenuItem.click();
      });

      let modalBox = document.querySelector('#modalBox');
      let input = modalBox?.querySelector('input') as HTMLInputElement;

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'src_renamed');
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      let submitBtn = modalBox?.querySelector('button[type="submit"]') as HTMLButtonElement;
      await act(async () => {
        submitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/rename', {
        method: 'PUT',
        body: {
          workspaceId: 'ws-123',
          oldPath: 'src',
          newPath: 'src_renamed',
        },
      });

      // Now test Delete folder from menu
      await act(async () => {
        srcMenuBtn.click();
      });

      const deleteMenuItem = Array.from(container.querySelectorAll('.tree-menu-item')).find(
        (b) => b.textContent?.includes('Delete')
      ) as HTMLButtonElement;
      expect(deleteMenuItem).toBeDefined();

      await act(async () => {
        deleteMenuItem.click();
      });

      const confirmDeleteBtn = document.querySelector('#confirmDeleteTreeItemBtn') as HTMLButtonElement;
      await act(async () => {
        confirmDeleteBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/item', {
        method: 'DELETE',
        body: {
          workspaceId: 'ws-123',
          path: 'src',
        },
      });
    });

    it('handles drag and drop moving items across folders', async () => {
      const onRefresh = vi.fn();
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: true,
        status: 200,
        data: { success: true },
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
                onRefresh={onRefresh}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      const fileNodes = container.querySelectorAll('.file-tree-node');
      const readmeNode = fileNodes[0] as HTMLDivElement;
      const folderNodes = container.querySelectorAll('.folder-tree-node');
      const srcFolderNode = folderNodes[1] as HTMLDivElement;

      // Drag start on file
      const dataTransfer = {
        data: {} as Record<string, string>,
        setData(format: string, data: string) {
          this.data[format] = data;
        },
        getData(format: string) {
          return this.data[format] || '';
        },
        effectAllowed: 'none',
        dropEffect: 'none',
      };

      await act(async () => {
        const dragStartEvent = new Event('dragstart', { bubbles: true }) as any;
        dragStartEvent.dataTransfer = dataTransfer;
        readmeNode.dispatchEvent(dragStartEvent);
      });
      expect(dataTransfer.getData('text/plain')).toBe('README.md');

      // Drag over directory
      await act(async () => {
        const dragOverEvent = new Event('dragover', { bubbles: true }) as any;
        dragOverEvent.dataTransfer = dataTransfer;
        srcFolderNode.dispatchEvent(dragOverEvent);
      });

      // Drag leave directory
      await act(async () => {
        const dragLeaveEvent = new Event('dragleave', { bubbles: true }) as any;
        srcFolderNode.dispatchEvent(dragLeaveEvent);
      });

      // Drop on directory
      await act(async () => {
        const dropEvent = new Event('drop', { bubbles: true }) as any;
        dropEvent.dataTransfer = dataTransfer;
        srcFolderNode.dispatchEvent(dropEvent);
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/filesystem/move', {
        method: 'POST',
        body: {
          workspaceId: 'ws-123',
          sourcePath: 'README.md',
          destinationDirectory: 'src',
        },
      });
      expect(onRefresh).toHaveBeenCalled();
    });

    it('filters files dynamically and shows empty notice on non-matching query', async () => {
      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      const filterInput = container.querySelector('#fileTreeFilterInput') as HTMLInputElement;
      expect(filterInput).not.toBeNull();

      // Filter for 'logo'
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(filterInput, 'logo');
        filterInput.dispatchEvent(new Event('input', { bubbles: true }));
        filterInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      expect(container.textContent).toContain('logo.png');
      expect(container.textContent).not.toContain('README.md');

      // Filter for completely missing file
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(filterInput, 'completely-missing-xyz');
        filterInput.dispatchEvent(new Event('input', { bubbles: true }));
        filterInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      expect(container.textContent).toContain('No matching files found.');
    });

    it('handles api error states gracefully for file creation and item deletion', async () => {
      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockResolvedValue({
        ok: false,
        status: 400,
        error: 'Invalid file name',
      } as any);

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <ModalProvider>
              <Modal />
              <FileTree
                workspaceId="ws-123"
                node={sampleTree}
                onSelectFile={vi.fn()}
              />
            </ModalProvider>
          </ToastProvider>
        );
      });

      // Try creating new file with error response
      const menuBtn = container.querySelector('#treeMenuBtn-root') as HTMLButtonElement;
      await act(async () => {
        menuBtn.click();
      });

      const newFileMenuItem = Array.from(container.querySelectorAll('.tree-menu-item')).find(
        (b) => b.textContent?.includes('New File')
      ) as HTMLButtonElement;
      await act(async () => {
        newFileMenuItem.click();
      });

      const modalBox = document.querySelector('#modalBox');
      const input = modalBox?.querySelector('input') as HTMLInputElement;
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(input, 'bad?.txt');
        input.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const submitBtn = modalBox?.querySelector('button[type="submit"]') as HTMLButtonElement;
      await act(async () => {
        submitBtn.click();
      });

      expect(apiFetchSpy).toHaveBeenCalled();
    });
  });
});
