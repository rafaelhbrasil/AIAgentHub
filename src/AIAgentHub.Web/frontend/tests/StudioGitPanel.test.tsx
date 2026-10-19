import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { StudioGitPanel, GitNotReadyNotice, CreateBranchModal } from '../src/components/workspaces/studio/StudioGitPanel';
import { GitStatusDto } from '../src/types/git';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';

describe('StudioGitPanel and Subcomponents', () => {
  describe('GitNotReadyNotice', () => {
    it('renders warning when git is not installed', () => {
      const status: GitStatusDto = {
        isGitInstalled: false,
        isRepository: false,
        currentBranch: '',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(<GitNotReadyNotice status={status} />);
      expect(html).toContain('Git Not Detected');
      expect(html).toContain('not available on PATH');
    });

    it('renders warning when folder is not a git repository', () => {
      const status: GitStatusDto = {
        isGitInstalled: true,
        isRepository: false,
        currentBranch: '',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(<GitNotReadyNotice status={status} />);
      expect(html).toContain('Not a Git Repository');
    });

    it('renders null when repository is ready', () => {
      const status: GitStatusDto = {
        isGitInstalled: true,
        isRepository: true,
        currentBranch: 'main',
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      const html = renderToString(<GitNotReadyNotice status={status} />);
      expect(html).toBe('');
    });
  });

  describe('CreateBranchModal', () => {
    it('renders branch creation modal form', () => {
      const html = renderToString(
        <CreateBranchModal onSubmit={vi.fn()} onCancel={vi.fn()} />
      );

      expect(html).toContain('Branch Name');
      expect(html).toContain('gitModalBranchNameInput');
      expect(html).toContain('Create &amp; Checkout');
      expect(html).toContain('Cancel');
    });
  });

  describe('StudioGitPanel', () => {
    it('renders drawer header and controls when mounted and open', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <StudioGitPanel
              workspaceId="ws-456"
              isOpen={true}
              onClose={vi.fn()}
            />
          </ModalProvider>
        </ToastProvider>
      );

      expect(html).toContain('studio-git-drawer');
      expect(html).toContain('Git Control');
      expect(html).toContain('gitCloseDrawerBtn');
      expect(html).toContain('gitRefreshBtn');
      expect(html).toContain('Changes');
      expect(html).toContain('History');
      expect(html).toContain('gitCommitBtn');
    });

    it('returns empty string when isOpen is false', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <StudioGitPanel
              workspaceId="ws-456"
              isOpen={false}
              onClose={vi.fn()}
            />
          </ModalProvider>
        </ToastProvider>
      );

      expect(html).toBe('');
    });

    it('adds mobile-hidden class when mobileTab is not git', () => {
      const html = renderToString(
        <ToastProvider>
          <ModalProvider>
            <StudioGitPanel
              workspaceId="ws-456"
              isOpen={true}
              mobileTab="sidebar"
              onClose={vi.fn()}
            />
          </ModalProvider>
        </ToastProvider>
      );

      expect(html).toContain('studio-git-drawer');
      expect(html).toContain('mobile-hidden');
    });
  });
});
