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
  parents?: string[];
  refs?: string | null;
}

export interface GitCommitFileChangeDto {
  path: string;
  status: string;
  additions: number;
  deletions: number;
}

export interface GitCommitDetailDto {
  hash: string;
  shortHash: string;
  author: string;
  email: string;
  date: string;
  message: string;
  parents: string[];
  refs?: string | null;
  files: GitCommitFileChangeDto[];
}

export interface GitCommitDiffDto {
  path: string;
  originalContent?: string | null;
  modifiedContent?: string | null;
  isBinary: boolean;
}

export interface GitOperationResult {
  success: boolean;
  message?: string | null;
  error?: string | null;
}
