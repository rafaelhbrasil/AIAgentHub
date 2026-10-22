import React, { useState, useEffect, useRef, useCallback } from 'react';
import { apiFetch } from '../../services/apiClient';
import { FileContentResponse } from '../../types/workspace';
import { useToast } from '../../context/ToastContext';
import { Spinner } from '../common/Spinner';

interface CodeEditorModalProps {
  workspaceId: string;
  initialPath: string;
  workspaceFiles?: string[];
  onClose: () => void;
  onSaved?: (path: string) => void;
}

interface DiffLine {
  type: 'added' | 'removed' | 'unchanged';
  text: string;
  originalLineNum?: number;
  newLineNum?: number;
}

function findLookaheadMatch(origLine: string, newLines: string[], start: number, lookahead = 15): number {
  const limit = Math.min(start + lookahead, newLines.length);
  for (let j = start; j < limit; j++) {
    if (newLines[j] === origLine) return j;
  }
  return -1;
}

function pushAddedLines(result: DiffLine[], newLines: string[], fromIdx: number, toIdx: number): void {
  for (let i = fromIdx; i < toIdx; i++) {
    result.push({
      type: 'added',
      text: newLines[i],
      newLineNum: i + 1,
    });
  }
}

export function computeSimpleDiff(origText: string, newText: string): DiffLine[] {
  const origLines = origText.split(/\r?\n/);
  const newLines = newText.split(/\r?\n/);
  const result: DiffLine[] = [];

  let oIdx = 0;
  let nIdx = 0;

  while (oIdx < origLines.length || nIdx < newLines.length) {
    if (oIdx < origLines.length && nIdx < newLines.length && origLines[oIdx] === newLines[nIdx]) {
      result.push({
        type: 'unchanged',
        text: newLines[nIdx],
        originalLineNum: oIdx + 1,
        newLineNum: nIdx + 1,
      });
      oIdx++;
      nIdx++;
      continue;
    }

    const matchInNew = oIdx < origLines.length ? findLookaheadMatch(origLines[oIdx], newLines, nIdx + 1) : -1;
    if (matchInNew !== -1) {
      pushAddedLines(result, newLines, nIdx, matchInNew);
      nIdx = matchInNew;
      continue;
    }

    if (oIdx < origLines.length) {
      result.push({
        type: 'removed',
        text: origLines[oIdx],
        originalLineNum: oIdx + 1,
      });
      oIdx++;
    }
    if (nIdx < newLines.length && (oIdx >= origLines.length || origLines[oIdx] !== newLines[nIdx])) {
      result.push({
        type: 'added',
        text: newLines[nIdx],
        newLineNum: nIdx + 1,
      });
      nIdx++;
    }
  }

  return result;
}

export function getDiffLineStyle(type: 'added' | 'removed' | 'unchanged') {
  if (type === 'added') {
    return { bg: 'rgba(34, 197, 94, 0.15)', sign: '+', color: '#4ade80' };
  }
  if (type === 'removed') {
    return { bg: 'rgba(239, 68, 68, 0.15)', sign: '-', color: '#f87171' };
  }
  return { bg: 'transparent', sign: ' ', color: 'inherit' };
}

export function formatMatchCount(query: string, count: number): string {
  if (!query) return '';
  return `${count} match${count === 1 ? '' : 'es'}`;
}

export interface EditorDiffViewProps {
  diffLines: DiffLine[];
}

export const EditorDiffView: React.FC<EditorDiffViewProps> = ({ diffLines }) => {
  if (diffLines.length === 0) {
    return <div style={{ padding: '16px', color: 'var(--text-muted)' }}>No differences from disk.</div>;
  }
  return (
    <div
      className="editor-diff-view"
      style={{
        height: '100%',
        overflow: 'auto',
        fontFamily: 'var(--font-mono, monospace)',
        fontSize: '0.84rem',
        lineHeight: '1.5',
        padding: '8px 0',
        color: 'var(--text-main)',
      }}
    >
      {diffLines.map((line, idx) => {
        const { bg, sign, color } = getDiffLineStyle(line.type);
        const lineKey = `${line.type}-${line.originalLineNum ?? 0}-${line.newLineNum ?? 0}-${line.text.slice(0, 15)}-${idx}`;
        return (
          <div
            key={lineKey}
            style={{
              display: 'flex',
              background: bg,
              padding: '1px 8px',
              whiteSpace: 'pre',
            }}
          >
            <span
              style={{
                width: '38px',
                color: 'var(--text-muted)',
                textAlign: 'right',
                paddingRight: '8px',
                userSelect: 'none',
                opacity: 0.6,
              }}
            >
              {line.originalLineNum || ''}
            </span>
            <span
              style={{
                width: '38px',
                color: 'var(--text-muted)',
                textAlign: 'right',
                paddingRight: '8px',
                userSelect: 'none',
                opacity: 0.6,
              }}
            >
              {line.newLineNum || ''}
            </span>
            <span style={{ width: '16px', color, userSelect: 'none', textAlign: 'center' }}>
              {sign}
            </span>
            <span style={{ flex: 1, color, whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
              {line.text || ' '}
            </span>
          </div>
        );
      })}
    </div>
  );
};

function useEditorKeyboardShortcuts({
  onSave,
  onToggleFind,
  onToggleSwitcher,
  onCloseOverlays,
}: {
  onSave: () => void;
  onToggleFind: () => void;
  onToggleSwitcher: () => void;
  onCloseOverlays: () => void;
}) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const isCmdOrCtrl = e.ctrlKey || e.metaKey;
      if (isCmdOrCtrl && e.key.toLowerCase() === 's') {
        e.preventDefault();
        onSave();
        return;
      }
      if (isCmdOrCtrl && e.key.toLowerCase() === 'f') {
        e.preventDefault();
        onToggleFind();
        return;
      }
      if (isCmdOrCtrl && e.key.toLowerCase() === 'p') {
        e.preventDefault();
        onToggleSwitcher();
        return;
      }
      if (e.key === 'Escape') {
        onCloseOverlays();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onSave, onToggleFind, onToggleSwitcher, onCloseOverlays]);
}

export const CodeEditorModal: React.FC<CodeEditorModalProps> = ({
  workspaceId,
  initialPath,
  workspaceFiles = [],
  onClose,
  onSaved,
}) => {
  const { showToast } = useToast();
  const [currentPath, setCurrentPath] = useState<string>(initialPath);
  const [originalContent, setOriginalContent] = useState<string>('');
  const [content, setContent] = useState<string>('');
  const [isBinary, setIsBinary] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [isReadOnly, setIsReadOnly] = useState<boolean>(false);
  const [showDiff, setShowDiff] = useState<boolean>(false);

  // Find & Replace
  const [showFindReplace, setShowFindReplace] = useState<boolean>(false);
  const [findQuery, setFindQuery] = useState<string>('');
  const [replaceQuery, setReplaceQuery] = useState<string>('');
  const [matchCount, setMatchCount] = useState<number>(0);

  // Quick file switcher (Ctrl+P)
  const [showQuickSwitcher, setShowQuickSwitcher] = useState<boolean>(false);
  const [quickFilter, setQuickFilter] = useState<string>('');

  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const lineNumbersRef = useRef<HTMLDivElement>(null);
  const quickFilterInputRef = useRef<HTMLInputElement>(null);

  const isDirty = content !== originalContent;

  const loadFile = useCallback(
    async (path: string) => {
      setIsLoading(true);
      setShowDiff(false);
      try {
        const res = await apiFetch<FileContentResponse>(
          `/api/v1/filesystem/file?workspaceId=${workspaceId}&path=${encodeURIComponent(path)}`
        );
        if (res.ok && res.data) {
          if (res.data.isBinary) {
            setIsBinary(true);
            setContent('');
            setOriginalContent('');
          } else {
            setIsBinary(false);
            const text = res.data.content ?? '';
            setContent(text);
            setOriginalContent(text);
          }
          setCurrentPath(path);
        } else {
          showToast(res.error || 'Failed to load file content.', 'error');
        }
      } catch (err: any) {
        showToast(err.message || 'Error loading file.', 'error');
      } finally {
        setIsLoading(false);
      }
    },
    [workspaceId, showToast]
  );

  useEffect(() => {
    loadFile(initialPath);
  }, [initialPath, loadFile]);

  // Sync line numbers scroll with textarea
  const handleScroll = () => {
    if (textareaRef.current && lineNumbersRef.current) {
      lineNumbersRef.current.scrollTop = textareaRef.current.scrollTop;
    }
  };

  // Update match count for Find
  useEffect(() => {
    if (!findQuery) {
      setMatchCount(0);
      return;
    }
    try {
      const regex = new RegExp(findQuery.replace(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`), 'g');
      const matches = content.match(regex);
      setMatchCount(matches ? matches.length : 0);
    } catch {
      setMatchCount(0);
    }
  }, [findQuery, content]);

  // Save handler
  const handleSave = async () => {
    if (isReadOnly || isBinary || isSaving) return;
    setIsSaving(true);
    try {
      const res = await apiFetch('/api/v1/filesystem/file', {
        method: 'PUT',
        body: {
          workspaceId,
          path: currentPath,
          content,
        },
      });

      if (res.ok) {
        setOriginalContent(content);
        showToast(`Saved ${currentPath}`, 'success');
        onSaved?.(currentPath);
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to save file.', 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error saving file.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  // Find & Replace actions
  const handleReplaceNext = () => {
    if (!findQuery || !textareaRef.current) return;
    const idx = content.indexOf(findQuery);
    if (idx !== -1) {
      const updated = content.substring(0, idx) + replaceQuery + content.substring(idx + findQuery.length);
      setContent(updated);
    }
  };

  const handleReplaceAll = () => {
    if (!findQuery) return;
    const updated = content.split(findQuery).join(replaceQuery);
    setContent(updated);
    showToast(`Replaced all occurrences of "${findQuery}".`, 'info');
  };

  const handleCloseOverlays = useCallback(() => {
    if (showQuickSwitcher) {
      setShowQuickSwitcher(false);
    } else if (showFindReplace) {
      setShowFindReplace(false);
    }
  }, [showQuickSwitcher, showFindReplace]);

  useEditorKeyboardShortcuts({
    onSave: handleSave,
    onToggleFind: () => setShowFindReplace((prev) => !prev),
    onToggleSwitcher: () => setShowQuickSwitcher((prev) => !prev),
    onCloseOverlays: handleCloseOverlays,
  });

  // Focus quick filter when opened
  useEffect(() => {
    if (showQuickSwitcher) {
      setTimeout(() => quickFilterInputRef.current?.focus(), 50);
    }
  }, [showQuickSwitcher]);

  // Handle Tab key in editor
  const handleKeyDownInEditor = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Tab') {
      e.preventDefault();
      const target = e.currentTarget;
      const start = target.selectionStart;
      const end = target.selectionEnd;
      const spaces = '  ';
      const updated = content.substring(0, start) + spaces + content.substring(end);
      setContent(updated);
      setTimeout(() => {
        target.selectionStart = target.selectionEnd = start + spaces.length;
      }, 0);
    }
  };

  const handleSelectQuickFile = (filePath: string) => {
    if (isDirty) {
      if (!window.confirm(`You have unsaved changes in "${currentPath}". Discard changes and switch?`)) {
        return;
      }
    }
    setShowQuickSwitcher(false);
    setQuickFilter('');
    loadFile(filePath);
  };

  const lines = content.split('\n');
  const lineCount = lines.length;

  const filteredQuickFiles = workspaceFiles.filter((f) =>
    f.toLowerCase().includes(quickFilter.toLowerCase())
  );

  const diffLines = showDiff ? computeSimpleDiff(originalContent, content) : [];
  const lineNumbers = Array.from({ length: lineCount }, (_, i) => i + 1);

  const renderWorkspaceContent = () => {
    if (isLoading) {
      return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%', color: 'var(--text-muted)' }}>
          <Spinner size={24} isSpinning={true} />
          <span style={{ marginLeft: '10px' }}>Loading {currentPath}...</span>
        </div>
      );
    }
    if (isBinary) {
      return (
        <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', height: '100%', color: 'var(--text-muted)' }}>
          <p style={{ fontSize: '1.1rem', marginBottom: '8px' }}>⚠️ Binary File</p>
          <p style={{ fontSize: '0.85rem' }}>This file contains binary data and cannot be displayed in the text editor.</p>
        </div>
      );
    }
    if (showDiff) {
      return <EditorDiffView diffLines={diffLines} />;
    }
    return (
      <div style={{ display: 'flex', height: '100%', overflow: 'hidden' }}>
        <div
          ref={lineNumbersRef}
          style={{
            width: '48px',
            background: '#181825',
            color: '#6c7086',
            fontFamily: 'var(--font-mono, monospace)',
            fontSize: '0.84rem',
            lineHeight: '1.5',
            textAlign: 'right',
            padding: '8px 8px 8px 0',
            userSelect: 'none',
            overflowY: 'hidden',
            borderRight: '1px solid rgba(255,255,255,0.07)',
            flexShrink: 0,
          }}
        >
          {lineNumbers.map((num) => (
            <div key={`line-num-${num}`}>{num}</div>
          ))}
        </div>

        <textarea
          ref={textareaRef}
          value={content}
          onChange={(e) => setContent(e.target.value)}
          onScroll={handleScroll}
          onKeyDown={handleKeyDownInEditor}
          readOnly={isReadOnly}
          spellCheck={false}
          id="editorMainTextarea"
          style={{
            flex: 1,
            height: '100%',
            background: '#1e1e2e',
            color: '#cdd6f4',
            fontFamily: 'var(--font-mono, monospace)',
            fontSize: '0.84rem',
            lineHeight: '1.5',
            padding: '8px 12px',
            border: 'none',
            outline: 'none',
            resize: 'none',
            whiteSpace: 'pre',
            overflowWrap: 'normal',
            overflowX: 'auto',
            tabSize: 2,
          }}
        />
      </div>
    );
  };

  return (
    <div className="code-editor-container" style={{ display: 'flex', flexDirection: 'column', height: '80vh', minHeight: '500px' }}>
      {/* Top Action Bar */}
      <div
        className="editor-top-bar"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '8px 12px',
          background: 'var(--bg-card)',
          borderBottom: '1px solid var(--border-color)',
          flexWrap: 'wrap',
          gap: '8px',
        }}
      >
        {/* Left: Path, Dirty indicator, File Switcher Trigger */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', minWidth: 0 }}>
          <strong
            style={{
              fontSize: '0.92rem',
              color: 'var(--text-heading)',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              maxWidth: '350px',
            }}
            title={currentPath}
          >
            {currentPath} {isDirty && <span style={{ color: '#f59e0b' }} title="Unsaved changes">*</span>}
          </strong>

          {workspaceFiles.length > 0 && (
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={() => setShowQuickSwitcher((prev) => !prev)}
              style={{ fontSize: '0.78rem', padding: '3px 8px' }}
              title="Quick file switcher (Ctrl+P)"
              id="editorQuickSwitcherBtn"
            >
              🔄 Switch File <span style={{ opacity: 0.6, fontSize: '0.7rem' }}>Ctrl+P</span>
            </button>
          )}
        </div>

        {/* Right: Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <button
            type="button"
            className={`btn compact-btn ${showFindReplace ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setShowFindReplace((prev) => !prev)}
            style={{ fontSize: '0.78rem', padding: '3px 8px' }}
            title="Search and Replace (Ctrl+F)"
            id="editorFindReplaceBtn"
          >
            🔍 Find <span style={{ opacity: 0.6, fontSize: '0.7rem' }}>Ctrl+F</span>
          </button>

          <button
            type="button"
            className={`btn compact-btn ${showDiff ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setShowDiff((prev) => !prev)}
            style={{ fontSize: '0.78rem', padding: '3px 8px' }}
            title="Toggle inline comparison against disk"
            id="editorDiffToggleBtn"
          >
            ⚖️ {showDiff ? 'Editor View' : 'Disk Diff'}
          </button>

          <label
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '4px',
              fontSize: '0.78rem',
              color: 'var(--text-muted)',
              cursor: 'pointer',
              userSelect: 'none',
              marginLeft: '4px',
            }}
          >
            <input
              type="checkbox"
              checked={isReadOnly}
              onChange={(e) => setIsReadOnly(e.target.checked)}
              id="editorReadOnlyCheckbox"
            />
            <span>Read-only</span>
          </label>

          <button
            type="button"
            className="btn btn-primary compact-btn"
            onClick={handleSave}
            disabled={isReadOnly || isBinary || isSaving || !isDirty}
            style={{ fontSize: '0.78rem', padding: '3px 12px' }}
            title="Save file changes (Ctrl+S)"
            id="editorSaveBtn"
          >
            {isSaving ? 'Saving...' : '💾 Save'}
          </button>

          <button
            type="button"
            className="btn btn-secondary compact-btn"
            onClick={() => {
              if (isDirty) {
                if (!window.confirm('You have unsaved changes. Are you sure you want to close?')) return;
              }
              onClose();
            }}
            style={{ fontSize: '0.78rem', padding: '3px 10px' }}
            id="editorCloseBtn"
          >
            ✕
          </button>
        </div>
      </div>

      {/* Find & Replace Bar */}
      {showFindReplace && (
        <div
          className="find-replace-bar"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            padding: '6px 12px',
            background: 'var(--bg-subtle)',
            borderBottom: '1px solid var(--border-color)',
            flexWrap: 'wrap',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Find:</span>
            <input
              type="text"
              className="form-control"
              placeholder="Search..."
              value={findQuery}
              onChange={(e) => setFindQuery(e.target.value)}
              style={{ fontSize: '0.8rem', padding: '2px 6px', width: '160px' }}
              id="editorFindInput"
              autoFocus
            />
            <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)', minWidth: '60px' }}>
              {formatMatchCount(findQuery, matchCount)}
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Replace:</span>
            <input
              type="text"
              className="form-control"
              placeholder="Replace..."
              value={replaceQuery}
              onChange={(e) => setReplaceQuery(e.target.value)}
              style={{ fontSize: '0.8rem', padding: '2px 6px', width: '160px' }}
              id="editorReplaceInput"
            />
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={handleReplaceNext}
              disabled={!findQuery || isReadOnly}
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
              id="editorReplaceNextBtn"
            >
              Replace
            </button>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={handleReplaceAll}
              disabled={!findQuery || isReadOnly}
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
              id="editorReplaceAllBtn"
            >
              All
            </button>
          </div>
        </div>
      )}

      {/* Quick File Switcher Popover */}
      {showQuickSwitcher && (
        <div
          style={{
            position: 'absolute',
            top: '55px',
            left: '50%',
            transform: 'translateX(-50%)',
            width: '90%',
            maxWidth: '520px',
            background: 'var(--bg-card)',
            border: '1px solid var(--accent-primary)',
            boxShadow: '0 8px 30px rgba(0,0,0,0.5)',
            borderRadius: '8px',
            zIndex: 100,
            overflow: 'hidden',
          }}
        >
          <div style={{ padding: '8px', borderBottom: '1px solid var(--border-color)' }}>
            <input
              ref={quickFilterInputRef}
              type="text"
              className="form-control"
              placeholder="Type filename to jump to... (Press Esc to close)"
              value={quickFilter}
              onChange={(e) => setQuickFilter(e.target.value)}
              style={{ fontSize: '0.85rem' }}
              id="quickFileFilterInput"
            />
          </div>
          <div style={{ maxHeight: '240px', overflowY: 'auto' }}>
            {filteredQuickFiles.length === 0 ? (
              <div style={{ padding: '12px', fontSize: '0.8rem', color: 'var(--text-muted)' }}>
                No matching files.
              </div>
            ) : (
              filteredQuickFiles.slice(0, 30).map((f) => (
                <button
                  type="button"
                  key={f}
                  onClick={() => handleSelectQuickFile(f)}
                  style={{
                    padding: '6px 12px',
                    fontSize: '0.82rem',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px',
                    width: '100%',
                    border: 'none',
                    borderBottom: '1px solid rgba(255,255,255,0.04)',
                    background: f === currentPath ? 'rgba(99, 102, 241, 0.15)' : 'transparent',
                    color: 'inherit',
                    textAlign: 'left',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(255,255,255,0.06)')}
                  onMouseLeave={(e) =>
                    (e.currentTarget.style.background = f === currentPath ? 'rgba(99, 102, 241, 0.15)' : 'transparent')
                  }
                >
                  <span>📄</span>
                  <span style={{ flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {f}
                  </span>
                </button>
              ))
            )}
          </div>
        </div>
      )}

      {/* Main Workspace Area */}
      <div style={{ flex: 1, position: 'relative', overflow: 'hidden', background: '#1e1e2e' }}>
        {renderWorkspaceContent()}
      </div>

      {/* Editor Status Bar */}
      <div
        className="editor-status-bar"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '4px 12px',
          background: 'var(--bg-card)',
          borderTop: '1px solid var(--border-color)',
          fontSize: '0.75rem',
          color: 'var(--text-muted)',
        }}
      >
        <div>
          <span>Lines: {lineCount}</span>
          <span style={{ marginLeft: '12px' }}>Size: {content.length} characters</span>
          {isDirty && <span style={{ marginLeft: '12px', color: '#f59e0b' }}>● Modified</span>}
        </div>
        <div>
          <span>{isReadOnly ? '🔒 Read-Only' : '✏️ Editing'}</span>
          <span style={{ marginLeft: '12px' }}>UTF-8</span>
        </div>
      </div>
    </div>
  );
};
