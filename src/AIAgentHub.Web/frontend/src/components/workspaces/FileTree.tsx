import React, { useState, useMemo } from 'react';
import { FileTreeNode } from '../../types/workspace';
import { apiFetch } from '../../services/apiClient';
import { useToast } from '../../context/ToastContext';
import { useModal } from '../../context/ModalContext';

interface FileTreeProps {
  workspaceId: string;
  node: FileTreeNode | null;
  onSelectFile: (relativePath: string) => void;
  onOpenEditor?: (relativePath: string) => void;
  onRefresh?: () => void;
}

interface TreeNodeProps {
  workspaceId: string;
  node: FileTreeNode;
  onSelectFile: (relativePath: string) => void;
  onOpenEditor?: (relativePath: string) => void;
  onRefresh?: () => void;
  level?: number;
  forceExpand?: boolean;
}

export function filterTree(node: FileTreeNode, query: string): FileTreeNode | null {
  if (!query) return node;
  const lowerQuery = query.toLowerCase();
  const nameMatches = node.name.toLowerCase().includes(lowerQuery) ||
    (node.relativePath?.toLowerCase().includes(lowerQuery) ?? false);

  if (!node.isDirectory) {
    return nameMatches ? node : null;
  }

  const filteredChildren: FileTreeNode[] = [];
  if (node.children) {
    for (const child of node.children) {
      const filtered = filterTree(child, query);
      if (filtered) {
        filteredChildren.push(filtered);
      }
    }
  }

  if (nameMatches || filteredChildren.length > 0) {
    return {
      ...node,
      children: filteredChildren,
    };
  }

  return null;
}

export function getFolderBackground(isDragOver: boolean, isHovered: boolean, isRoot: boolean): string {
  if (isDragOver) return 'rgba(99, 102, 241, 0.25)';
  if (isHovered && !isRoot) return 'rgba(255, 255, 255, 0.06)';
  return 'transparent';
}

export function getFolderIcon(isRoot: boolean, isExpanded: boolean): string {
  if (isRoot) return '📁';
  return isExpanded ? '📂' : '📁';
}

function useTreeItemActions(
  workspaceId: string,
  node: FileTreeNode,
  onRefresh?: () => void,
  onOpenEditor?: (relativePath: string) => void
) {
  const { showToast } = useToast();
  const { showModal, hideModal } = useModal();

  const handleCreateFile = () => {
    const folderPath = node.relativePath || '';
    showModal(
      'Create New File',
      <CreateItemModal
        title={`New File in /${folderPath || 'root'}`}
        placeholder="e.g. index.ts, style.css"
        onSubmit={async (fileName) => {
          const separator = folderPath ? '/' : '';
          const targetPath = `${folderPath}${separator}${fileName}`;
          const res = await apiFetch('/api/v1/filesystem/file', {
            method: 'POST',
            body: { workspaceId, path: targetPath, content: '' },
          });
          if (res.ok) {
            showToast(`Created file "${targetPath}".`, 'success');
            hideModal();
            onRefresh?.();
            onOpenEditor?.(targetPath);
          } else {
            showToast((res.data as any)?.message || res.error || 'Failed to create file.', 'error');
          }
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleCreateDirectory = () => {
    const folderPath = node.relativePath || '';
    showModal(
      'Create New Folder',
      <CreateItemModal
        title={`New Folder in /${folderPath || 'root'}`}
        placeholder="e.g. components, utils"
        onSubmit={async (dirName) => {
          const separator = folderPath ? '/' : '';
          const targetPath = `${folderPath}${separator}${dirName}`;
          const res = await apiFetch('/api/v1/filesystem/mkdir', {
            method: 'POST',
            body: { workspaceId, path: targetPath },
          });
          if (res.ok) {
            showToast(`Created folder "${targetPath}".`, 'success');
            hideModal();
            onRefresh?.();
          } else {
            showToast((res.data as any)?.message || res.error || 'Failed to create folder.', 'error');
          }
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleRename = () => {
    const oldPath = node.relativePath;
    const parts = oldPath.split(/[/\\]/);
    const oldName = parts[parts.length - 1];

    showModal(
      `Rename ${node.isDirectory ? 'Folder' : 'File'}`,
      <CreateItemModal
        title={`Rename "${oldName}"`}
        defaultValue={oldName}
        placeholder="New name"
        buttonText="Rename"
        onSubmit={async (newName) => {
          parts[parts.length - 1] = newName;
          const newPath = parts.join('/');
          const res = await apiFetch('/api/v1/filesystem/rename', {
            method: 'PUT',
            body: { workspaceId, oldPath, newPath },
          });
          if (res.ok) {
            showToast(`Renamed to "${newName}".`, 'success');
            hideModal();
            onRefresh?.();
          } else {
            showToast((res.data as any)?.message || res.error || 'Failed to rename.', 'error');
          }
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleDelete = () => {
    showModal(
      `Delete ${node.isDirectory ? 'Folder' : 'File'}`,
      <div>
        <p>
          Are you sure you want to delete <strong>"{node.name}"</strong>?
        </p>
        <p style={{ color: 'var(--accent-danger)', fontSize: '0.85rem', marginTop: '8px' }}>
          ⚠️ This action will permanently delete this {node.isDirectory ? 'folder and all its contents' : 'file'} from your disk.
        </p>
      </div>,
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
        <button type="button" className="btn btn-secondary" onClick={hideModal}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-danger"
          id="confirmDeleteTreeItemBtn"
          onClick={async () => {
            const res = await apiFetch('/api/v1/filesystem/item', {
              method: 'DELETE',
              body: { workspaceId, path: node.relativePath },
            });
            hideModal();
            if (res.ok) {
              showToast(`Deleted "${node.name}".`, 'info');
              onRefresh?.();
            } else {
              showToast((res.data as any)?.message || res.error || 'Failed to delete.', 'error');
            }
          }}
        >
          Delete
        </button>
      </div>
    );
  };

  return { handleCreateFile, handleCreateDirectory, handleRename, handleDelete };
}

const TreeNode: React.FC<TreeNodeProps> = ({
  workspaceId,
  node,
  onSelectFile,
  onOpenEditor,
  onRefresh,
  level = 0,
  forceExpand = false,
}) => {
  const { showToast } = useToast();
  const isRoot = level === 0;
  const [isExpanded, setIsExpanded] = useState<boolean>(isRoot || forceExpand);
  const [isDragOver, setIsDragOver] = useState<boolean>(false);
  const [isHovered, setIsHovered] = useState<boolean>(false);
  const [showMenu, setShowMenu] = useState<boolean>(false);

  const { handleCreateFile, handleCreateDirectory, handleRename, handleDelete } =
    useTreeItemActions(workspaceId, node, onRefresh, onOpenEditor);

  // Sync expanded if forceExpand changes
  React.useEffect(() => {
    if (forceExpand) setIsExpanded(true);
  }, [forceExpand]);

  // Drag and drop handlers
  const handleDragStart = (e: React.DragEvent) => {
    if (isRoot) return;
    e.dataTransfer.setData('text/plain', node.relativePath);
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragOver = (e: React.DragEvent) => {
    if (!node.isDirectory) return;
    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = 'move';
    setIsDragOver(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    if (!node.isDirectory) return;
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);

    const sourcePath = e.dataTransfer.getData('text/plain');
    if (!sourcePath || sourcePath === node.relativePath) return;

    // Destination directory
    const destinationDirectory = node.relativePath || '';

    try {
      const res = await apiFetch('/api/v1/filesystem/move', {
        method: 'POST',
        body: {
          workspaceId,
          sourcePath,
          destinationDirectory,
        },
      });

      if (res.ok) {
        showToast(`Moved "${sourcePath}" into "/${destinationDirectory || 'root'}".`, 'success');
        onRefresh?.();
      } else {
        showToast((res.data as any)?.message || res.error || 'Failed to move item.', 'error');
      }
    } catch (err: any) {
      showToast(err.message || 'Error moving item.', 'error');
    }
  };

  if (!node.isDirectory) {
    return (
      <div
        className="tree-item file-tree-node"
        role="button"
        tabIndex={0}
        draggable={true}
        onDragStart={handleDragStart}
        onClick={() => onSelectFile(node.relativePath)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onSelectFile(node.relativePath);
          }
        }}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        title={node.relativePath}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: '6px',
          padding: '4px 8px',
          borderRadius: '4px',
          cursor: 'pointer',
          fontSize: '0.86rem',
          color: 'var(--text-main)',
          transition: 'background 0.15s ease',
          userSelect: 'none',
          background: isHovered ? 'rgba(255, 255, 255, 0.06)' : 'transparent',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', overflow: 'hidden' }}>
          <span style={{ fontSize: '0.85rem', flexShrink: 0 }}>📄</span>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {node.name}
          </span>
        </div>

        {/* Action icons on hover */}
        {isHovered && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '4px', flexShrink: 0 }}>
            {onOpenEditor && (
              <button
                type="button"
                className="btn-icon-plain"
                onClick={(e) => {
                  e.stopPropagation();
                  onOpenEditor(node.relativePath);
                }}
                title="Open in Code Editor"
                style={{ fontSize: '0.78rem', padding: '1px 3px' }}
              >
                📝
              </button>
            )}
            <button
              type="button"
              className="btn-icon-plain"
              onClick={(e) => {
                e.stopPropagation();
                handleRename();
              }}
              title="Rename file"
              style={{ fontSize: '0.78rem', padding: '1px 3px' }}
            >
              ✏️
            </button>
            <button
              type="button"
              className="btn-icon-plain"
              onClick={(e) => {
                e.stopPropagation();
                handleDelete();
              }}
              title="Delete file"
              style={{ fontSize: '0.78rem', padding: '1px 3px' }}
            >
              🗑️
            </button>
          </div>
        )}
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
        role="button"
        tabIndex={isRoot ? -1 : 0}
        draggable={!isRoot}
        onDragStart={handleDragStart}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={toggleExpand}
        onKeyDown={(e) => {
          if (!isRoot && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault();
            toggleExpand();
          }
        }}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        title={node.relativePath || node.name}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: '6px',
          padding: '5px 8px',
          borderRadius: '4px',
          cursor: isRoot ? 'default' : 'pointer',
          fontWeight: isRoot ? 700 : 500,
          fontSize: '0.88rem',
          color: isRoot ? 'var(--text-heading)' : 'var(--text-main)',
          userSelect: 'none',
          transition: 'background 0.15s ease, border 0.15s ease',
          background: getFolderBackground(isDragOver, isHovered, isRoot),
          border: isDragOver ? '1px dashed var(--accent-primary)' : '1px solid transparent',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', overflow: 'hidden' }}>
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
                flexShrink: 0,
              }}
            >
              ▶
            </span>
          )}
          <span style={{ fontSize: '0.9rem', flexShrink: 0 }}>
            {getFolderIcon(isRoot, isExpanded)}
          </span>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {node.name}
          </span>
        </div>

        {/* Directory Action Context Menu */}
        <div style={{ position: 'relative', display: 'flex', alignItems: 'center', flexShrink: 0 }}>
          <button
            type="button"
            className="btn-icon-plain"
            onClick={(e) => {
              e.stopPropagation();
              setShowMenu((prev) => !prev);
            }}
            title={`Actions for ${node.name}`}
            style={{ fontSize: '0.85rem', padding: '1px 6px', opacity: isHovered || showMenu ? 1 : 0.65 }}
            id={`treeMenuBtn-${node.name}`}
          >
            ⋯
          </button>

          {showMenu && (
            <>
              <button
                type="button"
                aria-label="Close menu"
                style={{ position: 'fixed', inset: 0, zIndex: 45, background: 'transparent', border: 'none', cursor: 'default' }}
                onClick={(e) => {
                  e.stopPropagation();
                  setShowMenu(false);
                }}
              />
              <div className="tree-menu-dropdown">
                <button
                  type="button"
                  className="tree-menu-item"
                  onClick={(e) => {
                    e.stopPropagation();
                    setShowMenu(false);
                    handleCreateFile();
                  }}
                >
                  📄 New File
                </button>
                <button
                  type="button"
                  className="tree-menu-item"
                  onClick={(e) => {
                    e.stopPropagation();
                    setShowMenu(false);
                    handleCreateDirectory();
                  }}
                >
                  📁 New Folder
                </button>
                {!isRoot && (
                  <>
                    <button
                      type="button"
                      className="tree-menu-item"
                      onClick={(e) => {
                        e.stopPropagation();
                        setShowMenu(false);
                        handleRename();
                      }}
                    >
                      ✏️ Rename
                    </button>
                    <button
                      type="button"
                      className="tree-menu-item text-danger"
                      onClick={(e) => {
                        e.stopPropagation();
                        setShowMenu(false);
                        handleDelete();
                      }}
                    >
                      🗑️ Delete
                    </button>
                  </>
                )}
              </div>
            </>
          )}
        </div>
      </div>

      {(isRoot || isExpanded) && hasChildren && (
        <div style={{ paddingLeft: isRoot ? '12px' : '18px' }}>
          {node.children!.map((child) => (
            <TreeNode
              key={child.relativePath || child.name}
              workspaceId={workspaceId}
              node={child}
              onSelectFile={onSelectFile}
              onOpenEditor={onOpenEditor}
              onRefresh={onRefresh}
              level={level + 1}
              forceExpand={forceExpand}
            />
          ))}
        </div>
      )}
    </div>
  );
};

// Modal for file/folder name input
interface CreateItemModalProps {
  title: string;
  placeholder?: string;
  defaultValue?: string;
  buttonText?: string;
  onSubmit: (name: string) => void;
  onCancel: () => void;
}

const CreateItemModal: React.FC<CreateItemModalProps> = ({
  title,
  placeholder,
  defaultValue = '',
  buttonText = 'Create',
  onSubmit,
  onCancel,
}) => {
  const [val, setVal] = useState<string>(defaultValue);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (val.trim()) {
      onSubmit(val.trim());
    }
  };

  return (
    <form onSubmit={handleSubmit} style={{ minWidth: '300px' }}>
      <div style={{ marginBottom: '14px', fontSize: '0.9rem', color: 'var(--text-heading)' }}>
        {title}
      </div>
      <div className="form-group" style={{ marginBottom: '16px' }}>
        <input
          type="text"
          className="form-control"
          placeholder={placeholder}
          value={val}
          onChange={(e) => setVal(e.target.value)}
          autoFocus
          required
        />
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn btn-primary" disabled={!val.trim()}>
          {buttonText}
        </button>
      </div>
    </form>
  );
};

export const FileTree: React.FC<FileTreeProps> = ({
  workspaceId,
  node,
  onSelectFile,
  onOpenEditor,
  onRefresh,
}) => {
  const [searchFilter, setSearchFilter] = useState<string>('');

  const filteredTree = useMemo(() => {
    if (!node) return null;
    return filterTree(node, searchFilter);
  }, [node, searchFilter]);

  if (!node) {
    return <p style={{ padding: '10px', color: 'var(--text-muted)' }}>Empty folder</p>;
  }

  return (
    <div className="file-tree-wrapper" style={{ display: 'flex', flexDirection: 'column' }}>
      {/* Search Filter Bar */}
      <div style={{ padding: '4px 8px 6px 8px' }}>
        <input
          type="text"
          className="form-control"
          placeholder="🔍 Filter files..."
          value={searchFilter}
          onChange={(e) => setSearchFilter(e.target.value)}
          style={{
            fontSize: '0.8rem',
            padding: '4px 8px',
            borderRadius: '4px',
            background: 'rgba(0,0,0,0.2)',
            border: '1px solid var(--border-color)',
          }}
          id="fileTreeFilterInput"
        />
      </div>

      {/* Tree Content */}
      <div
        className="tree-list"
        id="workspaceTree"
        style={{ padding: '4px 8px', overflowY: 'auto', maxHeight: '350px' }}
      >
        {filteredTree ? (
          <TreeNode
            workspaceId={workspaceId}
            node={filteredTree}
            onSelectFile={onSelectFile}
            onOpenEditor={onOpenEditor}
            onRefresh={onRefresh}
            level={0}
            forceExpand={Boolean(searchFilter)}
          />
        ) : (
          <p style={{ padding: '8px', fontSize: '0.82rem', color: 'var(--text-muted)' }}>
            No matching files found.
          </p>
        )}
      </div>
    </div>
  );
};
