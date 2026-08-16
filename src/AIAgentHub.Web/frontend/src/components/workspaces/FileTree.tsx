import React, { useState } from 'react';
import { FileTreeNode } from '../../types/workspace';

interface FileTreeProps {
  node: FileTreeNode | null;
  onSelectFile: (relativePath: string) => void;
}

interface TreeNodeProps {
  node: FileTreeNode;
  onSelectFile: (relativePath: string) => void;
  level?: number;
}

const TreeNode: React.FC<TreeNodeProps> = ({ node, onSelectFile, level = 0 }) => {
  // First level (level === 0) represents the workspace root: always expanded and cannot be collapsed.
  // Level >= 1 subfolders: collapsed by default (isExpanded = false) and can be toggled by the user.
  const isRoot = level === 0;
  const [isExpanded, setIsExpanded] = useState<boolean>(isRoot);

  if (!node.isDirectory) {
    return (
      <div
        className="tree-item file-tree-node"
        onClick={() => onSelectFile(node.relativePath)}
        title={node.relativePath}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          padding: '4px 8px',
          borderRadius: '4px',
          cursor: 'pointer',
          fontSize: '0.86rem',
          color: 'var(--text-main)',
          transition: 'background 0.15s ease, color 0.15s ease',
          userSelect: 'none',
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(255, 255, 255, 0.06)')}
        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
      >
        <span style={{ fontSize: '0.85rem' }}>📄</span>
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{node.name}</span>
      </div>
    );
  }

  // Directory node
  const hasChildren = Boolean(node.children && node.children.length > 0);

  const toggleExpand = () => {
    if (!isRoot) {
      setIsExpanded((prev) => !prev);
    }
  };

  return (
    <div style={{ marginBottom: '2px' }}>
      <div
        className={`tree-item folder-tree-node ${isRoot ? 'tree-root-folder' : ''}`}
        onClick={toggleExpand}
        title={node.relativePath || node.name}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          padding: '5px 8px',
          borderRadius: '4px',
          cursor: isRoot ? 'default' : 'pointer',
          fontWeight: isRoot ? 700 : 500,
          fontSize: '0.88rem',
          color: isRoot ? 'var(--text-heading)' : 'var(--text-main)',
          userSelect: 'none',
          transition: 'background 0.15s ease',
        }}
        onMouseEnter={(e) => {
          if (!isRoot) e.currentTarget.style.background = 'rgba(255, 255, 255, 0.06)';
        }}
        onMouseLeave={(e) => {
          if (!isRoot) e.currentTarget.style.background = 'transparent';
        }}
      >
        {!isRoot && (
          <span
            style={{
              display: 'inline-block',
              width: '14px',
              fontSize: '0.68rem',
              color: 'var(--text-muted)',
              transform: isExpanded ? 'rotate(90deg)' : 'none',
              transition: 'transform 0.15s ease',
              textAlign: 'center',
            }}
          >
            ▶
          </span>
        )}
        <span style={{ fontSize: '0.9rem' }}>
          {isRoot ? '📁' : isExpanded ? '📂' : '📁'}
        </span>
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flex: 1 }}>
          {node.name}
        </span>
      </div>

      {(isRoot || isExpanded) && hasChildren && (
        <div style={{ paddingLeft: isRoot ? '12px' : '18px' }}>
          {node.children!.map((child) => (
            <TreeNode
              key={child.relativePath || child.name}
              node={child}
              onSelectFile={onSelectFile}
              level={level + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
};

export const FileTree: React.FC<FileTreeProps> = ({ node, onSelectFile }) => {
  if (!node) {
    return <p style={{ padding: '10px', color: 'var(--text-muted)' }}>Empty folder</p>;
  }

  return (
    <div className="tree-list" id="workspaceTree" style={{ padding: '8px', overflowY: 'auto', maxHeight: '350px' }}>
      <TreeNode node={node} onSelectFile={onSelectFile} level={0} />
    </div>
  );
};
