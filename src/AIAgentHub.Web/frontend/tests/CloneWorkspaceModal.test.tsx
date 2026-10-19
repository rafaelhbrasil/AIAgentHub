import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { CloneWorkspaceModal, extractRepoName } from '../src/components/modals/CloneWorkspaceModal';
import { ToastProvider } from '../src/context/ToastContext';

describe('CloneWorkspaceModal', () => {
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
});
