import React, { useState, useMemo } from 'react';
import { GitCommitDto, GitStatusDto, GitBranchDto } from '../../../../types/git';
import { computeCommitGraph, GraphRow } from './gitGraphUtils';
import { GitCommitGraphCell } from './GitCommitGraphCell';
import { GitBranchIcon } from '../../../common/Icons';
import { Spinner } from '../../../common/Spinner';

export interface GitCommitLogTableProps {
  commits: GitCommitDto[];
  status: GitStatusDto | null;
  selectedRowId: string;
  onSelectRow: (id: string) => void;
  branches: GitBranchDto[];
  onCheckoutBranch: (name: string) => void;
  onCreateBranch: () => void;
  onFetch: () => void;
  onPull: () => void;
  onPush: () => void;
  onRefresh: () => void;
  isExecuting?: boolean;
  isLoading?: boolean;
}

export const GitCommitLogTable: React.FC<GitCommitLogTableProps> = ({
  commits,
  status,
  selectedRowId,
  onSelectRow,
  branches,
  onCheckoutBranch,
  onCreateBranch,
  onFetch,
  onPull,
  onPush,
  onRefresh,
  isExecuting = false,
  isLoading = false,
}) => {
  const [filterQuery, setFilterQuery] = useState('');

  // Filter commits based on search query
  const filteredCommits = useMemo(() => {
    if (!filterQuery.trim()) return commits;
    const q = filterQuery.toLowerCase();
    return commits.filter(
      (c) =>
        c.message.toLowerCase().includes(q) ||
        c.author.toLowerCase().includes(q) ||
        c.shortHash.toLowerCase().includes(q) ||
        (c.refs && c.refs.toLowerCase().includes(q))
    );
  }, [commits, filterQuery]);

  // Compute graph rows for visual lanes
  const graphRows = useMemo(() => {
    return computeCommitGraph(filteredCommits, true);
  }, [filteredCommits]);

  // Map row id -> GraphRow for O(1) lookup
  const graphRowMap = useMemo(() => {
    const map = new Map<string, GraphRow>();
    for (const r of graphRows) {
      map.set(r.id, r);
    }
    return map;
  }, [graphRows]);

  const workingTreeRow = graphRowMap.get('working-tree') || {
    id: 'working-tree',
    lane: 0,
    color: '#22c55e',
    maxLane: 0,
    upLinks: [],
    downLinks: [],
    passLanes: [],
    isWorkingTree: true,
  };

  const stagedCount = status?.staged.length || 0;
  const modifiedCount = status?.modified.length || 0;
  const untrackedCount = status?.untracked.length || 0;
  const totalChanges = stagedCount + modifiedCount + untrackedCount;

  const renderRefBadges = (refs?: string | null) => {
    if (!refs) return null;
    const parts = refs.split(',').map((p) => p.trim()).filter(Boolean);
    return (
      <span className="git-ref-badges" style={{ display: 'inline-flex', gap: '4px', marginRight: '6px' }}>
        {parts.map((ref, idx) => {
          let badgeStyle = {
            background: 'rgba(34, 197, 94, 0.15)',
            border: '1px solid rgba(34, 197, 94, 0.4)',
            color: '#4ade80',
          };

          if (ref.startsWith('HEAD ->')) {
            badgeStyle = {
              background: 'rgba(56, 189, 248, 0.15)',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              color: '#38bdf8',
            };
          } else if (ref.includes('tag:')) {
            badgeStyle = {
              background: 'rgba(250, 204, 21, 0.15)',
              border: '1px solid rgba(250, 204, 21, 0.4)',
              color: '#facc15',
            };
          } else if (ref.startsWith('origin/')) {
            badgeStyle = {
              background: 'rgba(167, 139, 250, 0.15)',
              border: '1px solid rgba(167, 139, 250, 0.4)',
              color: '#a78bfa',
            };
          }

          return (
            <span
              key={idx}
              className="badge"
              style={{
                fontSize: '0.7rem',
                padding: '1px 6px',
                borderRadius: '4px',
                fontWeight: 600,
                ...badgeStyle,
              }}
            >
              {ref}
            </span>
          );
        })}
      </span>
    );
  };

  const formatCommitDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString(undefined, {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return dateStr;
    }
  };

  return (
    <div className="git-workbench-top-pane" style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* Top Toolbar */}
      <div
        className="git-workbench-toolbar"
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: '8px',
          padding: '8px 12px',
          background: 'var(--bg-subtle)',
          borderBottom: '1px solid var(--border-color)',
        }}
      >
        {/* Left: Branch controls & remote sync */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
          <div style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
            <GitBranchIcon size={14} style={{ color: '#4ade80' }} />
            <select
              className="form-select compact-select"
              value={status?.currentBranch || ''}
              onChange={(e) => onCheckoutBranch(e.target.value)}
              disabled={isExecuting}
              style={{ maxWidth: '200px', fontSize: '0.8rem', fontWeight: 600 }}
              id="workbenchBranchSelect"
              title="Switch Git Branch"
            >
              {status?.currentBranch && !branches.some((b) => b.name === status.currentBranch) && (
                <option key={status.currentBranch} value={status.currentBranch}>
                  {status.currentBranch} (current)
                </option>
              )}
              {branches.map((b) => (
                <option key={b.name} value={b.name}>
                  {b.name} {b.isCurrent ? '(current)' : ''}
                </option>
              ))}
            </select>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={onCreateBranch}
              disabled={isExecuting}
              title="Create new branch"
              id="workbenchNewBranchBtn"
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
            >
              + New
            </button>
          </div>

          <div style={{ width: '1px', height: '18px', background: 'var(--border-color)', margin: '0 4px' }} />

          {/* Sync Stats & Actions */}
          <div style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
            {status && (status.ahead > 0 || status.behind > 0) ? (
              <span style={{ fontSize: '0.75rem', fontWeight: 600 }}>
                {status.ahead > 0 && <span style={{ color: '#4ade80', marginRight: '6px' }}>↑{status.ahead}</span>}
                {status.behind > 0 && <span style={{ color: '#f87171' }}>↓{status.behind}</span>}
              </span>
            ) : (
              <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Synced</span>
            )}

            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={onFetch}
              disabled={isExecuting}
              title="Fetch remote changes"
              id="workbenchFetchBtn"
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
            >
              Fetch
            </button>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={onPull}
              disabled={isExecuting}
              title="Pull changes from remote"
              id="workbenchPullBtn"
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
            >
              ⬇️ Pull
            </button>
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              onClick={onPush}
              disabled={isExecuting || !status?.ahead}
              title="Push local commits to remote"
              id="workbenchPushBtn"
              style={{ fontSize: '0.75rem', padding: '2px 8px' }}
            >
              ⬆️ Push
            </button>
          </div>
        </div>

        {/* Right: Search filter & refresh */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <input
            type="search"
            className="form-control compact-input git-workbench-filter-input"
            placeholder="Filter commits (hash, author, message)..."
            value={filterQuery}
            onChange={(e) => setFilterQuery(e.target.value)}
            id="workbenchCommitFilter"
          />
          <button
            type="button"
            className="btn-icon-plain"
            onClick={onRefresh}
            disabled={isExecuting || isLoading}
            title="Refresh Git log and status"
            id="workbenchRefreshBtn"
          >
            {isLoading ? <Spinner size={14} isSpinning={true} /> : '🔄'}
          </button>
        </div>
      </div>

      {/* Table Container */}
      <div className="git-workbench-table-wrap" style={{ flex: 1, overflowY: 'auto', overflowX: 'auto' }}>
        <table className="git-log-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.8rem' }}>
          <thead style={{ position: 'sticky', top: 0, background: 'var(--bg-surface)', zIndex: 2, borderBottom: '1px solid var(--border-color)' }}>
            <tr>
              <th style={{ width: '60px', padding: '6px 8px', color: 'var(--text-muted)', fontWeight: 600 }}>Graph</th>
              <th style={{ width: '80px', padding: '6px 8px', color: 'var(--text-muted)', fontWeight: 600 }}>Commit</th>
              <th style={{ padding: '6px 8px', color: 'var(--text-muted)', fontWeight: 600 }}>Description</th>
              <th className="hide-on-mobile" style={{ width: '140px', padding: '6px 8px', color: 'var(--text-muted)', fontWeight: 600 }}>Author</th>
              <th className="hide-on-mobile" style={{ width: '140px', padding: '6px 8px', color: 'var(--text-muted)', fontWeight: 600 }}>Date</th>
            </tr>
          </thead>
          <tbody>
            {/* ROW 0: Working Tree Changes (always top row) */}
            <tr
              onClick={() => onSelectRow('working-tree')}
              className={`git-log-row ${selectedRowId === 'working-tree' ? 'selected' : ''}`}
              style={{
                cursor: 'pointer',
                background: selectedRowId === 'working-tree' ? 'rgba(56, 189, 248, 0.12)' : 'rgba(34, 197, 94, 0.04)',
                borderBottom: '1px solid var(--border-color)',
              }}
              id="row-working-tree"
            >
              <td style={{ padding: '2px 8px', verticalAlign: 'middle' }}>
                <GitCommitGraphCell row={workingTreeRow} rowHeight={28} laneWidth={16} />
              </td>
              <td style={{ padding: '6px 8px', verticalAlign: 'middle' }}>
                <span
                  style={{
                    fontFamily: 'monospace',
                    fontSize: '0.75rem',
                    color: totalChanges > 0 ? '#4ade80' : 'var(--text-muted)',
                    fontWeight: 700,
                  }}
                >
                  ● UNCOMMITTED
                </span>
              </td>
              <td style={{ padding: '6px 8px', verticalAlign: 'middle' }}>
                <strong style={{ color: totalChanges > 0 ? '#4ade80' : 'var(--text-muted)', marginRight: '6px' }}>
                  Working Tree Changes
                </strong>
                {totalChanges > 0 ? (
                  <span className="git-working-tree-summary" style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                    ({stagedCount} staged, {modifiedCount} modified{untrackedCount > 0 ? `, ${untrackedCount} untracked` : ''})
                  </span>
                ) : (
                  <span className="git-working-tree-summary" style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                    (Clean working directory)
                  </span>
                )}
              </td>
              <td className="hide-on-mobile" style={{ padding: '6px 8px', verticalAlign: 'middle', color: 'var(--text-muted)' }}>
                You
              </td>
              <td className="hide-on-mobile" style={{ padding: '6px 8px', verticalAlign: 'middle', color: 'var(--text-muted)' }}>
                Current State
              </td>
            </tr>

            {/* Historical Commit Rows */}
            {filteredCommits.length === 0 ? (
              <tr>
                <td colSpan={5} style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)' }}>
                  {filterQuery ? 'No commits match the filter query.' : 'No commit history found in this repository.'}
                </td>
              </tr>
            ) : (
              filteredCommits.map((c) => {
                const rowData = graphRowMap.get(c.hash) || {
                  id: c.hash,
                  lane: 0,
                  color: '#38bdf8',
                  maxLane: 0,
                  upLinks: [],
                  downLinks: [],
                  passLanes: [],
                };
                const isSelected = selectedRowId === c.hash;

                return (
                  <tr
                    key={c.hash}
                    onClick={() => onSelectRow(c.hash)}
                    className={`git-log-row ${isSelected ? 'selected' : ''}`}
                    style={{
                      cursor: 'pointer',
                      background: isSelected ? 'rgba(56, 189, 248, 0.12)' : 'transparent',
                      borderBottom: '1px solid var(--border-color)',
                      transition: 'background 0.1s ease',
                    }}
                    id={`commit-row-${c.shortHash}`}
                  >
                    <td style={{ padding: '2px 8px', verticalAlign: 'middle' }}>
                      <GitCommitGraphCell row={rowData} rowHeight={28} laneWidth={16} />
                    </td>
                    <td style={{ padding: '6px 8px', verticalAlign: 'middle' }}>
                      <code
                        style={{
                          fontFamily: 'monospace',
                          fontSize: '0.75rem',
                          color: 'var(--accent-primary)',
                        }}
                      >
                        {c.shortHash}
                      </code>
                    </td>
                    <td style={{ padding: '6px 8px', verticalAlign: 'middle' }}>
                      <div style={{ display: 'flex', alignItems: 'center', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {renderRefBadges(c.refs)}
                        <span style={{ fontWeight: 500, color: 'var(--text-heading)' }} title={c.message}>
                          {c.message}
                        </span>
                      </div>
                    </td>
                    <td className="hide-on-mobile" style={{ padding: '6px 8px', verticalAlign: 'middle', color: 'var(--text-muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={c.author}>
                      {c.author}
                    </td>
                    <td className="hide-on-mobile" style={{ padding: '6px 8px', verticalAlign: 'middle', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
                      {formatCommitDate(c.date)}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
