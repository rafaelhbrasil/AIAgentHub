import { describe, it, expect } from 'vitest';
import { GitStatusDto, GitBranchDto, GitCommitDto } from '../src/types/git';
import { UpdateUsernameRequest, UpdatePasswordRequest } from '../src/types/settings';
import { WorkspaceOrigin } from '../src/types/workspace';

describe('v0.3 Types and Validation', () => {
  describe('WorkspaceOrigin enum', () => {
    it('has expected enum values for Server, Upload, and Git', () => {
      expect(WorkspaceOrigin.Server).toBe(0);
      expect(WorkspaceOrigin.Upload).toBe(1);
      expect(WorkspaceOrigin.Git).toBe(2);
    });
  });

  describe('GitStatusDto structure', () => {
    it('constructs a valid git status object', () => {
      const status: GitStatusDto = {
        isGitInstalled: true,
        isRepository: true,
        currentBranch: 'feature/v0.3',
        ahead: 2,
        behind: 0,
        staged: [{ path: 'src/App.tsx', status: 'M', isStaged: true }],
        modified: [{ path: 'README.md', status: 'M', isStaged: false }],
        untracked: ['scratch.txt'],
      };

      expect(status.isGitInstalled).toBe(true);
      expect(status.isRepository).toBe(true);
      expect(status.currentBranch).toBe('feature/v0.3');
      expect(status.ahead).toBe(2);
      expect(status.behind).toBe(0);
      expect(status.staged).toHaveLength(1);
      expect(status.modified).toHaveLength(1);
      expect(status.untracked).toHaveLength(1);
    });

    it('handles non-git or uninstalled scenarios safely', () => {
      const status: GitStatusDto = {
        isGitInstalled: false,
        isRepository: false,
        ahead: 0,
        behind: 0,
        staged: [],
        modified: [],
        untracked: [],
      };

      expect(status.isGitInstalled).toBe(false);
      expect(status.isRepository).toBe(false);
      expect(status.currentBranch).toBeUndefined();
    });

    it('constructs valid GitBranchDto and GitCommitDto objects', () => {
      const branch: GitBranchDto = {
        name: 'feature/v0.3',
        isCurrent: true,
        isRemote: false,
      };
      expect(branch.name).toBe('feature/v0.3');
      expect(branch.isCurrent).toBe(true);

      const commit: GitCommitDto = {
        hash: 'abcdef1234567890',
        shortHash: 'abcdef1',
        author: 'Developer',
        email: 'dev@example.com',
        date: new Date().toISOString(),
        message: 'feat: add git integration',
      };
      expect(commit.shortHash).toBe('abcdef1');
      expect(commit.message).toContain('git integration');
    });

    it('validates UpdateUsernameRequest and UpdatePasswordRequest payloads', () => {
      const userReq: UpdateUsernameRequest = {
        currentPassword: 'Password123!',
        newUsername: 'new_admin',
      };
      expect(userReq.newUsername).toBe('new_admin');

      const passReq: UpdatePasswordRequest = {
        currentPassword: 'Password123!',
        newPassword: 'NewSecurePassword456!',
        confirmNewPassword: 'NewSecurePassword456!',
      };
      expect(passReq.newPassword).toBe(passReq.confirmNewPassword);
    });
  });

  describe('Account request validation rules', () => {
    const validateUsername = (name: string): { valid: boolean; error?: string } => {
      if (!name || name.trim().length === 0) return { valid: false, error: 'Username cannot be empty.' };
      const trimmed = name.trim();
      if (trimmed.length < 3 || trimmed.length > 50) return { valid: false, error: 'Username must be between 3 and 50 characters.' };
      return { valid: true };
    };

    const validatePassword = (curr: string, next: string, confirm: string): { valid: boolean; error?: string } => {
      if (!curr) return { valid: false, error: 'Current password is required.' };
      if (!next || next.length < 8) return { valid: false, error: 'New password must be at least 8 characters.' };
      if (next !== confirm) return { valid: false, error: 'New passwords do not match.' };
      return { valid: true };
    };

    it('validates username inputs correctly', () => {
      expect(validateUsername('admin').valid).toBe(true);
      expect(validateUsername('super_user.99').valid).toBe(true);
      expect(validateUsername('').valid).toBe(false);
      expect(validateUsername('ab').valid).toBe(false);
      expect(validateUsername('a'.repeat(51)).valid).toBe(false);
    });

    it('validates password update inputs correctly', () => {
      expect(validatePassword('oldPass123', 'newSecret456', 'newSecret456').valid).toBe(true);
      expect(validatePassword('', 'newSecret456', 'newSecret456').valid).toBe(false);
      expect(validatePassword('oldPass123', 'short', 'short').valid).toBe(false);
      expect(validatePassword('oldPass123', 'newSecret456', 'differentPassword').valid).toBe(false);
    });
  });
});
