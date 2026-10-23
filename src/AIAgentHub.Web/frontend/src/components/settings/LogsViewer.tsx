import React, { useState, useEffect, useRef, useMemo } from 'react';
import { apiFetch } from '../../services/apiClient';
import { useToast } from '../../context/ToastContext';
import { LogFileSummaryDto, LogTailDto } from '../../types/logs';

export const LogsViewer: React.FC = () => {
  const { showToast } = useToast();
  const [files, setFiles] = useState<LogFileSummaryDto[]>([]);
  const [selectedFileName, setSelectedFileName] = useState<string>('');
  const [tailLines, setTailLines] = useState<string[]>([]);
  const [lineLimit, setLineLimit] = useState<number>(200);
  const [levelFilter, setLevelFilter] = useState<'ALL' | 'ERROR' | 'WARN' | 'INFO'>('ALL');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [autoScroll, setAutoScroll] = useState<boolean>(true);

  const [isLoadingFiles, setIsLoadingFiles] = useState<boolean>(true);
  const [isLoadingTail, setIsLoadingTail] = useState<boolean>(false);
  const [isDownloading, setIsDownloading] = useState<boolean>(false);

  const consoleEndRef = useRef<HTMLDivElement>(null);
  const consoleBodyRef = useRef<HTMLDivElement>(null);

  // Load available log files
  const loadFiles = async (keepSelection = false) => {
    setIsLoadingFiles(true);
    try {
      const res = await apiFetch<LogFileSummaryDto[]>('/api/v1/logs');
      if (res.ok && res.data) {
        setFiles(res.data);
        if (res.data.length > 0) {
          if (!keepSelection || !res.data.some((f) => f.fileName === selectedFileName)) {
            const currentDay = res.data.find((f) => f.isCurrentDay);
            setSelectedFileName(currentDay ? currentDay.fileName : res.data[0].fileName);
          }
        } else {
          setSelectedFileName('');
          setTailLines([]);
        }
      } else {
        showToast('Failed to load log files list.', 'error');
      }
    } catch {
      showToast('Network error while retrieving log files.', 'error');
    } finally {
      setIsLoadingFiles(false);
    }
  };

  // Load tail for selected file
  const loadTail = async (fileName: string, lines: number) => {
    if (!fileName) return;
    setIsLoadingTail(true);
    try {
      const res = await apiFetch<LogTailDto>(`/api/v1/logs/${encodeURIComponent(fileName)}/tail?lines=${lines}`);
      if (res.ok && res.data) {
        setTailLines(res.data.lines || []);
      } else {
        showToast(`Failed to load log tail for ${fileName}.`, 'error');
      }
    } catch {
      showToast(`Network error loading tail for ${fileName}.`, 'error');
    } finally {
      setIsLoadingTail(false);
    }
  };

  // Initial load
  useEffect(() => {
    loadFiles(false);
  }, []);

  // When selected file or lineLimit changes, fetch tail
  useEffect(() => {
    if (selectedFileName) {
      loadTail(selectedFileName, lineLimit);
    }
  }, [selectedFileName, lineLimit]);

  // Auto scroll to bottom when lines change
  useEffect(() => {
    if (autoScroll && consoleBodyRef.current) {
      consoleBodyRef.current.scrollTop = consoleBodyRef.current.scrollHeight;
    }
  }, [tailLines, autoScroll]);

  // Download selected file
  const handleDownload = async () => {
    if (!selectedFileName) return;
    setIsDownloading(true);
    try {
      const downloadUrl = `/api/v1/logs/${encodeURIComponent(selectedFileName)}/download`;
      const res = await fetch(downloadUrl, { credentials: 'include' });
      if (!res.ok) {
        showToast(`Download failed (${res.status} ${res.statusText})`, 'error');
        return;
      }

      const blob = await res.blob();
      const objectUrl = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = selectedFileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(objectUrl);
      showToast(`Downloaded ${selectedFileName}`, 'success');
    } catch {
      showToast(`Failed to download ${selectedFileName}`, 'error');
    } finally {
      setIsDownloading(false);
    }
  };

  const getLineLevel = (line: string): 'ERROR' | 'WARN' | 'INFO' | 'DEBUG' | 'DEFAULT' => {
    if (/\[ERR\]|\[ERROR\]|fail:|fatal:|exception/i.test(line)) return 'ERROR';
    if (/\[WRN\]|\[WARN\]|warn:/i.test(line)) return 'WARN';
    if (/\[INF\]|\[INFO\]|info:/i.test(line)) return 'INFO';
    if (/\[DBG\]|\[DEBUG\]|\[TRC\]|\[TRACE\]|dbug:/i.test(line)) return 'DEBUG';
    return 'DEFAULT';
  };

  const getLineColor = (level: ReturnType<typeof getLineLevel>): string => {
    switch (level) {
      case 'ERROR':
        return 'var(--accent-danger)';
      case 'WARN':
        return 'var(--accent-warning)';
      case 'INFO':
        return 'var(--accent-secondary)';
      case 'DEBUG':
        return 'var(--text-muted)';
      default:
        return 'var(--text-main)';
    }
  };

  // Filter lines by level and text query
  const filteredLines = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return tailLines.filter((line) => {
      const level = getLineLevel(line);

      if (levelFilter === 'ERROR' && level !== 'ERROR') return false;
      if (levelFilter === 'WARN' && level !== 'WARN' && level !== 'ERROR') return false;
      if (levelFilter === 'INFO' && level !== 'INFO' && level !== 'WARN' && level !== 'ERROR') return false;

      if (query && !line.toLowerCase().includes(query)) {
        return false;
      }

      return true;
    });
  }, [tailLines, levelFilter, searchQuery]);

  const selectedFileSummary = files.find((f) => f.fileName === selectedFileName);

  return (
    <div className="card glass" style={{ maxWidth: '1100px', marginTop: '16px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '12px', marginBottom: '16px' }}>
        <div>
          <div className="card-title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span>Diagnostic & Application Logs</span>
            {selectedFileSummary && (
              <span style={{ fontSize: '0.8rem', fontWeight: 'normal', color: 'var(--text-muted)' }}>
                ({selectedFileSummary.sizeFormatted})
              </span>
            )}
          </div>
          <p className="card-subtitle" style={{ margin: 0 }}>
            Inspect live tail output and download structured daily application logs.
          </p>
        </div>

        {/* Action Controls */}
        <div style={{ display: 'flex', gap: '8px', alignItems: 'center', flexWrap: 'wrap' }}>
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            onClick={() => {
              loadFiles(true);
              if (selectedFileName) loadTail(selectedFileName, lineLimit);
            }}
            disabled={isLoadingFiles || isLoadingTail}
            title="Refresh logs"
            style={{ fontSize: '0.84rem', padding: '6px 12px' }}
          >
            {isLoadingTail ? '⏳ Loading...' : '🔄 Refresh'}
          </button>

          <button
            type="button"
            className="btn btn-primary compact-btn"
            onClick={handleDownload}
            disabled={!selectedFileName || isDownloading}
            title={`Download ${selectedFileName}`}
            style={{ fontSize: '0.84rem', padding: '6px 14px' }}
          >
            {isDownloading ? '⬇️ Downloading...' : '⬇️ Download Log'}
          </button>
        </div>
      </div>

      {/* Toolbar: File selector, Lines selector, Filter chips & Search */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '12px',
          alignItems: 'center',
          background: 'var(--bg-subtle)',
          border: '1px solid var(--border-color)',
          borderRadius: 'var(--radius-sm)',
          padding: '10px 14px',
          marginBottom: '14px',
        }}
      >
        {/* File Dropdown */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label htmlFor="logFileSelect" style={{ fontSize: '0.82rem', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
            Log File:
          </label>
          <select
            id="logFileSelect"
            className="form-select"
            value={selectedFileName}
            onChange={(e) => setSelectedFileName(e.target.value)}
            disabled={isLoadingFiles || files.length === 0}
            style={{ fontSize: '0.84rem', padding: '4px 10px', minWidth: '220px' }}
          >
            {files.map((file) => (
              <option key={file.fileName} value={file.fileName}>
                {file.fileName} {file.isCurrentDay ? '(Today)' : `(${file.date})`} — {file.sizeFormatted}
              </option>
            ))}
            {files.length === 0 && <option value="">No log files found</option>}
          </select>
        </div>

        {/* Lines Dropdown */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label htmlFor="logTailSelect" style={{ fontSize: '0.82rem', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
            Tail:
          </label>
          <select
            id="logTailSelect"
            className="form-select"
            value={lineLimit}
            onChange={(e) => setLineLimit(Number(e.target.value))}
            style={{ fontSize: '0.84rem', padding: '4px 8px', width: '100px' }}
          >
            <option value={100}>100 lines</option>
            <option value={200}>200 lines</option>
            <option value={500}>500 lines</option>
            <option value={1000}>1000 lines</option>
          </select>
        </div>

        {/* Level Filters */}
        <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
          {(['ALL', 'ERROR', 'WARN', 'INFO'] as const).map((lvl) => (
            <button
              key={lvl}
              type="button"
              className={`btn compact-btn ${levelFilter === lvl ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setLevelFilter(lvl)}
              style={{
                fontSize: '0.75rem',
                padding: '3px 9px',
                borderRadius: '4px',
              }}
            >
              {lvl === 'ALL' ? 'All' : lvl === 'ERROR' ? 'Errors' : lvl === 'WARN' ? 'Warnings' : 'Info'}
            </button>
          ))}
        </div>

        {/* Search Input */}
        <div style={{ flex: '1 1 180px', minWidth: '150px' }}>
          <input
            type="text"
            className="form-input"
            placeholder="Search lines..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              fontSize: '0.82rem',
              padding: '4px 10px',
              width: '100%',
              boxSizing: 'border-box',
            }}
          />
        </div>

        {/* Auto-scroll toggle */}
        <label
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '0.8rem',
            color: 'var(--text-muted)',
            cursor: 'pointer',
            userSelect: 'none',
            whiteSpace: 'nowrap',
          }}
        >
          <input
            type="checkbox"
            checked={autoScroll}
            onChange={(e) => setAutoScroll(e.target.checked)}
          />
          {' '}
          Auto-scroll
        </label>
      </div>

      {/* Terminal Console View */}
      <div
        ref={consoleBodyRef}
        style={{
          fontFamily: 'var(--font-mono)',
          fontSize: '0.82rem',
          lineHeight: '1.5',
          background: 'rgba(10, 13, 20, 0.92)',
          border: '1px solid var(--border-color)',
          borderRadius: 'var(--radius-sm)',
          padding: '12px 14px',
          minHeight: '380px',
          maxHeight: '560px',
          overflowY: 'auto',
          overflowX: 'auto',
          boxShadow: 'inset 0 2px 6px rgba(0,0,0,0.4)',
        }}
      >
        {isLoadingTail && tailLines.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', padding: '20px', textAlign: 'center' }}>
            Loading log entries...
          </div>
        ) : files.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', padding: '20px', textAlign: 'center' }}>
            No log files found in server logs directory.
          </div>
        ) : filteredLines.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', padding: '20px', textAlign: 'center' }}>
            {tailLines.length > 0
              ? 'No lines match the active filter criteria.'
              : 'Log file is currently empty.'}
          </div>
        ) : (
          filteredLines.map((line, idx) => {
            const level = getLineLevel(line);
            const color = getLineColor(level);

            return (
              <div
                key={idx}
                style={{
                  display: 'flex',
                  gap: '12px',
                  color: color,
                  whiteSpace: 'pre-wrap',
                  wordBreak: 'break-all',
                }}
              >
                <span
                  style={{
                    color: 'rgba(255, 255, 255, 0.25)',
                    userSelect: 'none',
                    textAlign: 'right',
                    minWidth: '36px',
                  }}
                >
                  {idx + 1}
                </span>
                <span style={{ flex: 1 }}>{line}</span>
              </div>
            );
          })
        )}
        <div ref={consoleEndRef} />
      </div>

      {/* Footer Info */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginTop: '10px',
          fontSize: '0.78rem',
          color: 'var(--text-muted)',
        }}
      >
        <span>
          Showing {filteredLines.length} of {tailLines.length} tail lines
          {searchQuery && ` (matching "${searchQuery}")`}
        </span>
        {selectedFileSummary && (
          <span>
            File: <code>{selectedFileSummary.fileName}</code> &bull; Last updated:{' '}
            {new Date(selectedFileSummary.lastModifiedUtc).toLocaleTimeString()}
          </span>
        )}
      </div>
    </div>
  );
};
