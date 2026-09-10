export interface LogFileSummaryDto {
  fileName: string;
  date: string;
  sizeBytes: number;
  sizeFormatted: string;
  lastModifiedUtc: string;
  isCurrentDay: boolean;
}

export interface LogTailDto {
  fileName: string;
  totalLines: number;
  lines: string[];
}
