export interface GitFileStatusDto {
  path: string;
  status: string;
  isStaged: boolean;
}

export interface GitStatusDto {
  isGitInstalled: boolean;
  isRepository: boolean;
  currentBranch?: string | null;
  ahead: number;
  behind: number;
  staged: GitFileStatusDto[];
  modified: GitFileStatusDto[];
  untracked: string[];
}

export interface GitBranchDto {
  name: string;
  isCurrent: boolean;
  isRemote: boolean;
}

export interface GitCommitDto {
  hash: string;
  shortHash: string;
  author: string;
  email: string;
  date: string;
  message: string;
}

export interface GitOperationResult {
  success: boolean;
  message?: string | null;
  error?: string | null;
}
