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
interface LaneState {
  hash: string;
  color: string;
}

function getColor(laneIndex: number): string {
  return GRAPH_COLORS[laneIndex % GRAPH_COLORS.length];
}

function resolveCommitLane(commitHash: string, activeLanes: LaneState[]): { lane: number; color: string } {
  const existingLane = activeLanes.findIndex((l) => l.hash === commitHash);
  if (existingLane !== -1) {
    return { lane: existingLane, color: activeLanes[existingLane].color };
  }
  let emptyLane = activeLanes.findIndex((l) => !l.hash);
  if (emptyLane === -1) {
    emptyLane = activeLanes.length;
  }
  return { lane: emptyLane, color: getColor(emptyLane) };
}

function computeUpLinks(prevRow: GraphRow | undefined, targetLane: number, color: string): GraphLink[] {
  if (!prevRow) return [];
  if (prevRow.isWorkingTree) {
    return [{ fromLane: 0, toLane: targetLane, color }];
  }
  const matchingDown = prevRow.downLinks.find((d) => d.toLane === targetLane);
  return matchingDown ? [{ fromLane: matchingDown.fromLane, toLane: targetLane, color: matchingDown.color }] : [];
}

function allocateMergeParent(
  parentHash: string,
  nextActiveLanes: LaneState[]
): { lane: number; color: string } {
  const existingLane = nextActiveLanes.findIndex((l) => l.hash === parentHash);
  if (existingLane !== -1) {
    return { lane: existingLane, color: nextActiveLanes[existingLane].color };
  }
  let emptyLane = nextActiveLanes.findIndex((l) => !l.hash);
  if (emptyLane === -1) {
    emptyLane = nextActiveLanes.length;
  }
  const color = getColor(emptyLane);
  nextActiveLanes[emptyLane] = { hash: parentHash, color };
  return { lane: emptyLane, color };
}

function computeDownLinks(
  parents: string[],
  currentLane: number,
  currentColor: string,
  nextActiveLanes: LaneState[]
): { downLinks: GraphLink[]; localMaxLane: number } {
  const downLinks: GraphLink[] = [];
  let localMaxLane = currentLane;

  if (parents.length > 0) {
    nextActiveLanes[currentLane] = { hash: parents[0], color: currentColor };
    downLinks.push({ fromLane: currentLane, toLane: currentLane, color: currentColor });

    for (let p = 1; p < parents.length; p++) {
      const { lane: parentLane, color: parentColor } = allocateMergeParent(parents[p], nextActiveLanes);
      if (parentLane > localMaxLane) {
        localMaxLane = parentLane;
      }
      downLinks.push({ fromLane: currentLane, toLane: parentLane, color: parentColor });
    }
  }

  return { downLinks, localMaxLane };
}

function computePassLanes(activeLanes: LaneState[], currentLane: number): Array<{ lane: number; color: string }> {
  const passLanes: Array<{ lane: number; color: string }> = [];
  for (let l = 0; l < activeLanes.length; l++) {
    if (l !== currentLane && activeLanes[l]?.hash) {
      passLanes.push({ lane: l, color: activeLanes[l].color });
    }
  }
  return passLanes;
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
  let activeLanes: LaneState[] = [];

  if (includeWorkingTree) {
    const headHash = commits.length > 0 ? commits[0].hash : null;
    const wtColor = '#22c55e';
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
    if (headHash) {
      activeLanes = [{ hash: headHash, color: getColor(0) }];
    }
  }

  let globalMaxLane = 0;

  for (const commit of commits) {
    const { lane, color } = resolveCommitLane(commit.hash, activeLanes);
    if (lane > globalMaxLane) globalMaxLane = lane;

    const upLinks = computeUpLinks(rows[rows.length - 1], lane, color);
    const nextActiveLanes: LaneState[] = [...activeLanes];
    if (lane < nextActiveLanes.length) {
      nextActiveLanes[lane] = { hash: '', color };
    }

    const { downLinks, localMaxLane } = computeDownLinks(
      commit.parents || [],
      lane,
      color,
      nextActiveLanes
    );
    if (localMaxLane > globalMaxLane) globalMaxLane = localMaxLane;

    const passLanes = computePassLanes(activeLanes, lane);
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

  for (const r of rows) {
    r.maxLane = Math.max(r.maxLane, globalMaxLane);
  }

  return rows;
}
