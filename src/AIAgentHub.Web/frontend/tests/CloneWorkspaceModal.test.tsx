import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { CloneWorkspaceModal, extractRepoName } from '../src/components/modals/CloneWorkspaceModal';
import { ProviderDto, ProviderStatus } from '../src/types/provider';
import { ToastProvider } from '../src/context/ToastContext';
import * as apiClient from '../src/services/apiClient';

describe('CloneWorkspaceModal', () => {
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

  const mockProviders: ProviderDto[] = [
    {
      id: 'claude-code',
      displayName: 'Claude Code',
      status: ProviderStatus.Ready,
      isInstalled: true,
      description: 'Claude CLI',
      capabilities: 0,
      supportedModels: [],
    },
    {
      id: 'opencode',
      displayName: 'OpenCode',
      status: ProviderStatus.NotInstalled,
      isInstalled: false,
      description: 'OpenCode CLI',
      capabilities: 0,
      supportedModels: [],
    },
  ];

  describe('extractRepoName helper', () => {
    it('returns empty string for empty input', () => {
      expect(extractRepoName('')).toBe('');
      expect(extractRepoName('   ')).toBe('');
    });

    it('extracts name from standard https git urls', () => {
      expect(extractRepoName('https://github.com/torvalds/linux.git')).toBe('linux');
      expect(extractRepoName('https://github.com/torvalds/linux')).toBe('linux');
      expect(extractRepoName('https://gitlab.com/group/subgroup/project.git/')).toBe('project');
    });

    it('extracts name from ssh git urls', () => {
      expect(extractRepoName('git@github.com:facebook/react.git')).toBe('react');
      expect(extractRepoName('user@server.com:repo-name')).toBe('repo-name');
    });

    it('extracts name from windows or local paths', () => {
      expect(extractRepoName('C:\\Code\\MyRepo.git')).toBe('MyRepo');
      expect(extractRepoName('/var/git/project.git')).toBe('project');
    });
  });

  describe('component rendering', () => {
    it('renders the clone form inputs and buttons', () => {
      const html = renderToString(
        <ToastProvider>
          <CloneWorkspaceModal onSuccess={vi.fn()} onCancel={vi.fn()} />
        </ToastProvider>
      );

      expect(html).toContain('Clone a remote Git repository');
      expect(html).toContain('Git Repository URL');
      expect(html).toContain('Target Directory');
      expect(html).toContain('Workspace Name');
      expect(html).toContain('Cancel');
      expect(html).toContain('Clone &amp; Open');
    });
  });

  describe('Interactive Lifecycle & Form Submissions', () => {
    it('loads providers on mount, auto-suggests workspace name from url, and submits clone', async () => {
      const onSuccess = vi.fn();
      const onCancel = vi.fn();

      const apiFetchSpy = vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string, opts?: any) => {
        if (opts?.method === 'POST') {
          return {
            ok: true,
            status: 200,
            data: { id: 'ws-cloned-123', name: 'my-cool-repo' },
          } as any;
        }
        if (url.includes('/api/v1/providers')) {
          return {
            ok: true,
            status: 200,
            data: mockProviders,
          } as any;
        }
        return { ok: true, status: 200, data: null } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <CloneWorkspaceModal onSuccess={onSuccess} onCancel={onCancel} />
          </ToastProvider>
        );
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/providers');

      // Change URL -> should auto-suggest workspace name
      const urlInput = container.querySelector('#gitCloneUrl') as HTMLInputElement;
      const targetDirInput = container.querySelector('#gitTargetDir') as HTMLInputElement;
      const wsNameInput = container.querySelector('#gitWsName') as HTMLInputElement;

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(urlInput, 'https://github.com/myorg/my-cool-repo.git');
        urlInput.dispatchEvent(new Event('input', { bubbles: true }));
        urlInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      expect(wsNameInput.value).toBe('my-cool-repo');

      // Fill in target directory
      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(targetDirInput, 'D:\\Repos\\my-cool-repo');
        targetDirInput.dispatchEvent(new Event('input', { bubbles: true }));
        targetDirInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      // Select provider
      const providerSelect = container.querySelector('#gitDefaultProvider') as HTMLSelectElement;
      if (providerSelect) {
        await act(async () => {
          providerSelect.value = 'claude-code';
          providerSelect.dispatchEvent(new Event('change', { bubbles: true }));
        });
      }

      // Submit form
      const form = container.querySelector('form') as HTMLFormElement;
      await act(async () => {
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      });

      expect(apiFetchSpy).toHaveBeenCalledWith('/api/v1/workspaces/clone', {
        method: 'POST',
        body: {
          cloneUrl: 'https://github.com/myorg/my-cool-repo.git',
          targetDirectory: 'D:\\Repos\\my-cool-repo',
          workspaceName: 'my-cool-repo',
          defaultProviderId: 'claude-code',
        },
      });

      expect(onSuccess).toHaveBeenCalledWith('ws-cloned-123');
    });

    it('handles clone API failure response gracefully', async () => {
      vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'POST') {
          return {
            ok: false,
            status: 400,
            error: 'Repository not found or authentication failed.',
          } as any;
        }
        return { ok: true, status: 200, data: [] } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <CloneWorkspaceModal onSuccess={vi.fn()} onCancel={vi.fn()} />
          </ToastProvider>
        );
      });

      const urlInput = container.querySelector('#gitCloneUrl') as HTMLInputElement;
      const targetDirInput = container.querySelector('#gitTargetDir') as HTMLInputElement;

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(urlInput, 'https://github.com/private/repo.git');
        urlInput.dispatchEvent(new Event('change', { bubbles: true }));
        nativeSetter?.call(targetDirInput, 'D:\\Temp\\Repo');
        targetDirInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const form = container.querySelector('form') as HTMLFormElement;
      await act(async () => {
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      });

      // No crash, toast shown
    });

    it('handles network throw during clone gracefully', async () => {
      vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (_url: string, opts?: any) => {
        if (opts?.method === 'POST') {
          throw new Error('Connection reset by peer');
        }
        return { ok: true, status: 200, data: [] } as any;
      });

      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <CloneWorkspaceModal onSuccess={vi.fn()} onCancel={vi.fn()} />
          </ToastProvider>
        );
      });

      const urlInput = container.querySelector('#gitCloneUrl') as HTMLInputElement;
      const targetDirInput = container.querySelector('#gitTargetDir') as HTMLInputElement;

      await act(async () => {
        const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set;
        nativeSetter?.call(urlInput, 'https://github.com/private/repo.git');
        urlInput.dispatchEvent(new Event('change', { bubbles: true }));
        nativeSetter?.call(targetDirInput, 'D:\\Temp\\Repo');
        targetDirInput.dispatchEvent(new Event('change', { bubbles: true }));
      });

      const form = container.querySelector('form') as HTMLFormElement;
      await act(async () => {
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      });

      // Handled smoothly
    });

    it('cancels clone when Cancel button is clicked', async () => {
      const onCancel = vi.fn();
      root = createRoot(container);
      await act(async () => {
        root?.render(
          <ToastProvider>
            <CloneWorkspaceModal onSuccess={vi.fn()} onCancel={onCancel} />
          </ToastProvider>
        );
      });

      const cancelBtn = Array.from(container.querySelectorAll('button')).find((b) =>
        b.textContent?.includes('Cancel')
      ) as HTMLButtonElement;
      expect(cancelBtn).toBeDefined();

      await act(async () => {
        cancelBtn.click();
      });
      expect(onCancel).toHaveBeenCalled();
    });
  });
});
