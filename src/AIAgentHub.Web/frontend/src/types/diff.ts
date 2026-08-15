export enum DiffChangeType {
  Modified = 0,
  Created = 1,
  Deleted = 2
}

export interface SideBySideLine {
  leftLineNumber?: number | null;
  leftText?: string | null;
  leftKind: number; // 0=Unchanged, 1=Added, 2=Deleted, 3=Modified
  rightLineNumber?: number | null;
  rightText?: string | null;
  rightKind: number;
}

export interface FileChangeDto {
  id: string;
  conversationId: string;
  relativePath: string;
  changeType: DiffChangeType | number;
  isBinary: boolean;
  oldContent?: string | null;
  newContent?: string | null;
  sideBySideLines: SideBySideLine[];
}

export interface FilePreviewDto {
  relativePath: string;
  renderedHtml: string;
}
