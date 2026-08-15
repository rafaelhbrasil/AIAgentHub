import React from 'react';
import { FileTreeNode } from '../../types/workspace';

interface FileTreeProps {
  node: FileTreeNode | null;
  onSelectFile: (relativePath: string) => void;
}

const TreeNode: React.FC<{ node: FileTreeNode; onSelectFile: (relativePath: string) => void }> = ({
  node,
  onSelectFile,
}) => {
  if (!node.isDirectory) {
    return (
      <div
        className="tree-item file-tree-node"
        onClick={() => onSelectFile(node.relativePath)}
        title={node.relativePath}
      >
        📄 {node.name}
      </div>
    );
  }

  return (
    <div style={{ marginBottom: '4px' }}>
      <div className="tree-item" style={{ fontWeight: 600 }}>
        📁 {node.name}
      </div>
      <div style={{ paddingLeft: '14px' }}>
        {(node.children || []).map((child) => (
          <TreeNode key={child.relativePath} node={child} onSelectFile={onSelectFile} />
        ))}
      </div>
    </div>
  );
};

export const FileTree: React.FC<FileTreeProps> = ({ node, onSelectFile }) => {
  if (!node) {
    return <p style={{ padding: '10px', color: 'var(--text-muted)' }}>Empty folder</p>;
  }

  return (
    <div className="tree-list" id="workspaceTree">
      <TreeNode node={node} onSelectFile={onSelectFile} />
    </div>
  );
};
