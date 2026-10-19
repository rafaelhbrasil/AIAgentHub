import { GitCommitDto } from '../../../../types/git';

export const GRAPH_COLORS = [
  '#38bdf8', // sky
  '#4ade80', // emerald
  '#fb923c', // orange
  '#a78bfa', // purple
  '#f472b6', // pink
  '#facc15', // yellow
  '#2dd4bf', // teal
  '#e879f9', // fuchsia
];

export interface GraphLink {
  fromLane: number;
  toLane: number;
  color: string;
}

export interface GraphRow {
  id: string;
  lane: number;
  color: string;
  maxLane: number;
  upLinks: GraphLink[];
  downLinks: GraphLink[];
  passLanes: Array<{ lane: number; color: string }>;
  isWorkingTree?: boolean;
}

/**
 * Computes lane allocations and connecting paths for an ordered sequence of Git commits.
 * Supports an optional Working Tree row at index 0.
 */
export function computeCommitGraph(
  commits: GitCommitDto[],
  includeWorkingTree: boolean = true
): GraphRow[] {
  const rows: GraphRow[] = [];

  // Active branch tracking: laneIndex -> parentHash expected
  let activeLanes: Array<{ hash: string; color: string }> = [];

  const getColor = (laneIndex: number) =>
    GRAPH_COLORS[laneIndex % GRAPH_COLORS.length];

  // 1. Add Working Tree row if requested
  if (includeWorkingTree) {
    const headHash = commits.length > 0 ? commits[0].hash : null;
    const wtColor = '#22c55e'; // Green indicator for uncommitted working tree
    rows.push({
      id: 'working-tree',
      lane: 0,
      color: wtColor,
      maxLane: 0,
      upLinks: [],
      downLinks: headHash ? [{ fromLane: 0, toLane: 0, color: wtColor }] : [],
      passLanes: [],
      isWorkingTree: true,
    });
    // Prime activeLanes with HEAD on lane 0
    if (headHash) {
      activeLanes = [{ hash: headHash, color: getColor(0) }];
    }
  }

  let globalMaxLane = 0;

  for (let i = 0; i < commits.length; i++) {
    const commit = commits[i];
    const parents = commit.parents || [];

    // Find if this commit was expected on an active lane
    let lane = activeLanes.findIndex((l) => l.hash === commit.hash);
    let color: string;

    if (lane !== -1) {
      color = activeLanes[lane].color;
    } else {
      // Find lowest available lane
      lane = activeLanes.findIndex((l) => !l.hash);
      if (lane === -1) {
        lane = activeLanes.length;
      }
      color = getColor(lane);
    }

    if (lane > globalMaxLane) globalMaxLane = lane;

    // UpLinks: Connections from previous row entering this commit
    const upLinks: GraphLink[] = [];
    if (rows.length > 0) {
      const prevRow = rows[rows.length - 1];
      if (prevRow.isWorkingTree) {
        upLinks.push({ fromLane: 0, toLane: lane, color });
      } else {
        // Did prev commit link to this commit?
        const matchingDown = prevRow.downLinks.find((d) => d.toLane === lane);
        if (matchingDown) {
          upLinks.push({ fromLane: matchingDown.fromLane, toLane: lane, color: matchingDown.color });
        }
      }
    }

    // DownLinks: Connections leaving this commit towards its parents
    const downLinks: GraphLink[] = [];
    const nextActiveLanes: Array<{ hash: string; color: string }> = [...activeLanes];

    // Remove current commit from active lanes
    if (lane < nextActiveLanes.length) {
      nextActiveLanes[lane] = { hash: '', color };
    }

    if (parents.length === 0) {
      // Root commit (no parents)
    } else {
      // First parent inherits current lane
      const firstParent = parents[0];
      nextActiveLanes[lane] = { hash: firstParent, color };
      downLinks.push({ fromLane: lane, toLane: lane, color });

      // Additional parents (merges) get allocated or join existing lanes
      for (let p = 1; p < parents.length; p++) {
        const parentHash = parents[p];
        let parentLane = nextActiveLanes.findIndex((l) => l.hash === parentHash);
        let parentColor: string;

        if (parentLane !== -1) {
          parentColor = nextActiveLanes[parentLane].color;
        } else {
          parentLane = nextActiveLanes.findIndex((l) => !l.hash);
          if (parentLane === -1) parentLane = nextActiveLanes.length;
          parentColor = getColor(parentLane);
          nextActiveLanes[parentLane] = { hash: parentHash, color: parentColor };
        }

        if (parentLane > globalMaxLane) globalMaxLane = parentLane;
        downLinks.push({ fromLane: lane, toLane: parentLane, color: parentColor });
      }
    }

    // Pass-through lanes (lanes active in current row but not modified by this commit)
    const passLanes: Array<{ lane: number; color: string }> = [];
    for (let l = 0; l < activeLanes.length; l++) {
      if (l !== lane && activeLanes[l]?.hash) {
        passLanes.push({ lane: l, color: activeLanes[l].color });
      }
    }

    activeLanes = nextActiveLanes;

    rows.push({
      id: commit.hash,
      lane,
      color,
      maxLane: globalMaxLane,
      upLinks,
      downLinks,
      passLanes,
      isWorkingTree: false,
    });
  }

  // Backfill maxLane across all rows so column width is consistent
  for (const r of rows) {
    r.maxLane = Math.max(r.maxLane, globalMaxLane);
  }

  return rows;
}
