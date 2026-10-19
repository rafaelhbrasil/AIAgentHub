import React from 'react';
import { GraphRow } from './gitGraphUtils';

interface GitCommitGraphCellProps {
  row: GraphRow;
  rowHeight?: number;
  laneWidth?: number;
}

export const GitCommitGraphCell: React.FC<GitCommitGraphCellProps> = ({
  row,
  rowHeight = 28,
  laneWidth = 16,
}) => {
  const midY = rowHeight / 2;
  const nodeX = (row.lane + 0.5) * laneWidth + 6;
  const svgWidth = Math.max(36, (row.maxLane + 1) * laneWidth + 12);

  return (
    <svg
      width={svgWidth}
      height={rowHeight}
      style={{ display: 'block', overflow: 'visible' }}
      className="git-graph-svg"
      aria-label={`Git branch graph lane ${row.lane}`}
    >
      {/* 1. Pass-through lines for other active branches */}
      {row.passLanes.map((pass) => {
        const passX = (pass.lane + 0.5) * laneWidth + 6;
        return (
          <line
            key={`pass-${pass.lane}`}
            x1={passX}
            y1={0}
            x2={passX}
            y2={rowHeight}
            stroke={pass.color}
            strokeWidth={2}
            opacity={0.85}
          />
        );
      })}

      {/* 2. Up links (connections entering node from above) */}
      {row.upLinks.map((link, idx) => {
        const fromX = (link.fromLane + 0.5) * laneWidth + 6;
        if (fromX === nodeX) {
          return (
            <line
              key={`up-${idx}`}
              x1={nodeX}
              y1={0}
              x2={nodeX}
              y2={midY}
              stroke={link.color}
              strokeWidth={2}
            />
          );
        }
        return (
          <path
            key={`up-${idx}`}
            d={`M ${fromX} 0 C ${fromX} ${midY * 0.5}, ${nodeX} ${midY * 0.5}, ${nodeX} ${midY}`}
            fill="none"
            stroke={link.color}
            strokeWidth={2}
          />
        );
      })}

      {/* 3. Down links (connections leaving node towards parents) */}
      {row.downLinks.map((link, idx) => {
        const toX = (link.toLane + 0.5) * laneWidth + 6;
        if (nodeX === toX) {
          return (
            <line
              key={`down-${idx}`}
              x1={nodeX}
              y1={midY}
              x2={nodeX}
              y2={rowHeight}
              stroke={link.color}
              strokeWidth={2}
            />
          );
        }
        return (
          <path
            key={`down-${idx}`}
            d={`M ${nodeX} ${midY} C ${nodeX} ${midY + (rowHeight - midY) * 0.5}, ${toX} ${midY + (rowHeight - midY) * 0.5}, ${toX} ${rowHeight}`}
            fill="none"
            stroke={link.color}
            strokeWidth={2}
          />
        );
      })}

      {/* 4. Node circle marker */}
      {row.isWorkingTree ? (
        <g>
          {/* Working tree dashed pulse ring */}
          <circle
            cx={nodeX}
            cy={midY}
            r={6}
            fill="none"
            stroke="#22c55e"
            strokeWidth={1.5}
            strokeDasharray="2 2"
          />
          <circle
            cx={nodeX}
            cy={midY}
            r={3.5}
            fill="#22c55e"
          />
        </g>
      ) : (
        <circle
          cx={nodeX}
          cy={midY}
          r={4}
          fill={row.color}
          stroke="rgba(0, 0, 0, 0.4)"
          strokeWidth={1.5}
        />
      )}
    </svg>
  );
};
