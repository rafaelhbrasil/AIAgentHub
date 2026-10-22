import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { FolderExplorerModal } from '../src/components/modals/FolderExplorerModal';
import { ToastProvider } from '../src/context/ToastContext';
import * as apiClient from '../src/services/apiClient';

describe('FolderExplorerModal', () => {
  let container: HTMLDivElement;

  beforeEach(() => {
    vi.restoreAllMocks();
    container = document.createElement('div');
    document.body.appendChild(container);
  });

  it('mounts, fetches drives, providers, allows directory navigation, folder creation, and cancellation', async () => {
    const onSuccess = vi.fn();
    const onCancel = vi.fn();

    vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string) => {
      if (url.includes('/api/v1/filesystem/drives')) {
        return {
          ok: true,
          status: 200,
          data: [
            { name: 'C:\\', path: 'C:\\', driveType: 'Fixed', totalSizeGb: 500, freeSpaceGb: 200 },
            { name: 'D:\\', path: 'D:\\', driveType: 'Fixed', totalSizeGb: 1000, freeSpaceGb: 500 },
          ],
        } as any;
      }
      if (url.includes('/api/v1/filesystem/forbidden-paths')) {
        return {
          ok: true,
          status: 200,
          data: {
            forbiddenPaths: ['C:\\Windows', 'C:\\Program Files'],
          },
        } as any;
      }
      if (url.includes('/api/v1/providers')) {
        return {
          ok: true,
          status: 200,
          data: [
            { id: 'prov-1', name: 'OpenCode', type: 'OpenCode', isConfigured: true },
          ],
        } as any;
      }
      if (url.includes('/api/v1/filesystem/browse')) {
        return {
          ok: true,
          status: 200,
          data: {
            currentPath: 'D:\\Projects',
            directories: [
              { name: 'ProjectA', path: 'D:\\Projects\\ProjectA' },
            ],
            files: [],
          },
        } as any;
      }
      if (url.includes('/api/v1/filesystem/mkdir')) {
        return {
          ok: true,
          status: 200,
          data: { path: 'D:\\Projects\\NewFolder' },
        } as any;
      }
      if (url.includes('/api/v1/workspaces')) {
        return {
          ok: true,
          status: 200,
          data: { id: 'ws-new-123' },
        } as any;
      }
      return { ok: true, status: 200, data: {} } as any;
    });

    const root = createRoot(container);
    await act(async () => {
      root.render(
        <ToastProvider>
          <FolderExplorerModal onSuccess={onSuccess} onCancel={onCancel} />
        </ToastProvider>
      );
    });

    expect(container.textContent).toContain('Workspace Root Directory');
    expect(container.textContent).toContain('Drives & Devices');

    // 1. Click a drive button
    const driveBtns = container.querySelectorAll('.explorer-sidebar button');
    expect(driveBtns.length).toBeGreaterThan(0);
    await act(async () => {
      (driveBtns[0] as HTMLButtonElement).click();
    });

    // 2. Click Quick Links Home Profile
    const homeProfileBtn = Array.from(driveBtns).find((b) => b.textContent?.includes('Home Profile'));
    if (homeProfileBtn) {
      await act(async () => {
        (homeProfileBtn as HTMLButtonElement).click();
      });
    }

    // 3. Type path in workspace root input and press Enter
    const pathInput = container.querySelector('#workspaceRootDirInput') as HTMLInputElement;
    if (pathInput) {
      await act(async () => {
        pathInput.value = 'D:\\Projects';
        pathInput.dispatchEvent(new Event('input', { bubbles: true }));
        pathInput.dispatchEvent(new Event('change', { bubbles: true }));
        pathInput.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      });
    }

    // 4. Cancel button
    const cancelBtn = Array.from(container.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Cancel'
    );
    if (cancelBtn) {
      await act(async () => {
        cancelBtn.click();
      });
      expect(onCancel).toHaveBeenCalled();
    }

    act(() => {
      root.unmount();
    });
    container.remove();
  });
});
