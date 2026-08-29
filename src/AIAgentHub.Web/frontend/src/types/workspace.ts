export interface WorkspaceSettings {
  defaultProviderId?: string;
  defaultModelId?: string;
}

export interface WorkspaceDto {
  id: string;
  name: string;
  path: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  conversationCount: number;
  isFavorite?: boolean;
  isArchived?: boolean;
  settings?: WorkspaceSettings;
}

export interface FileTreeNode {
  name: string;
  relativePath: string;
  fullPath?: string;
  isDirectory: boolean;
  sizeBytes?: number;
  children?: FileTreeNode[];
}

export interface DriveDto {
  name: string;
  path: string;
  totalSizeBytes: number;
  freeSizeBytes: number;
  driveType?: string;
}

export interface DirectoryBrowserEntry {
  name: string;
  fullPath: string;
  isDirectory: boolean;
}

export interface DirectoryBrowserResult {
  currentPath: string;
  parentPath?: string | null;
  entries: DirectoryBrowserEntry[];
}

export interface ForbiddenPathsResponse {
  forbiddenPaths: string[];
}

