export enum DiffChangeType {
  Modified = 'Modified',
  Created = 'Created',
  Deleted = 'Deleted',
}

export interface SideBySideLine {
  leftLineNumber?: number | null;
  leftText?: string | null;
  leftKind: number; // 0=Unchanged, 1=Added, 2=Deleted, 3=Modified
  rightLineNumber?: number | null;
  rightText?: string | null;
  rightKind: number;
}

export interface UnifiedLine {
  oldLineNumber?: number | null;
  newLineNumber?: number | null;
  content: string;
  kind: number; // 0=Unchanged, 1=Added, 2=Deleted, 3=Modified
}

export interface FileChangeDto {
  id: string;
  conversationId: string;
  relativePath: string;
  changeType: DiffChangeType | string;
  isBinary: boolean;
  oldContent?: string | null;
  newContent?: string | null;
  additionsCount?: number;
  deletionsCount?: number;
  sideBySideLines?: SideBySideLine[];
  unifiedLines?: UnifiedLine[];
}

export interface FilePreviewDto {
  filePath?: string;
  rendererName?: string;
  contentType?: string;
  renderedHtml: string;
  rawText?: string | null;
  isBinary?: boolean;
  sizeBytes?: number;
}
