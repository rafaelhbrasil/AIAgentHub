import React from 'react';
import { FileTreeNode } from '../../../types/workspace';
import { ConversationDto, ConversationDetailDto } from '../../../types/conversation';
import { FileTree } from '../FileTree';
import { ConversationList } from '../ConversationList';
import { Spinner } from '../../common/Spinner';

interface StudioSidebarProps {
  mobileTab: 'chat' | 'sidebar';
  treeData: FileTreeNode | null;
  conversations: ConversationDto[];
  activeConversation: ConversationDetailDto | null;
  isRefreshingTree: boolean;
  onDownloadZip: () => void;
  onRefreshFiles: () => void;
  onPreviewFile: (path: string) => void;
  onCreateConversation: () => void;
  onSelectConversation: (id: string) => void;
  onDeleteConversation: (id: string, title: string) => void;
}

export const StudioSidebar: React.FC<StudioSidebarProps> = ({
  mobileTab,
  treeData,
  conversations,
  activeConversation,
  isRefreshingTree,
  onDownloadZip,
  onRefreshFiles,
  onPreviewFile,
  onCreateConversation,
  onSelectConversation,
  onDeleteConversation,
}) => {
  return (
    <div className={`sidebar-panel glass ${mobileTab === 'chat' ? 'mobile-hidden' : ''}`}>
      <div
        className="sidebar-header"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <strong>Files & Folders</strong>
        <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
          <button
            type="button"
            className="btn-refresh-icon"
            onClick={onDownloadZip}
            title="Download workspace project as ZIP"
            id="downloadWorkspaceZipBtn"
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              fontSize: '0.92rem',
              padding: '2px 4px',
              lineHeight: 1,
            }}
          >
            📥
          </button>
          <button
            type="button"
            className="btn-refresh-icon"
            onClick={onRefreshFiles}
            disabled={isRefreshingTree}
            title="Refresh Files & Folders structure"
            id="refreshFilesBtn"
          >
            <Spinner size={16} isSpinning={isRefreshingTree} />
          </button>
        </div>
      </div>
      <FileTree node={treeData} onSelectFile={onPreviewFile} />

      <div
        className="sidebar-header"
        style={{
          borderTop: '1px solid var(--border-color)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <strong>Conversations</strong>
          <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', fontWeight: 'normal' }}>
            ({conversations.length})
          </span>
        </div>
        <button
          type="button"
          className="btn btn-secondary compact-btn"
          style={{ padding: '2px 8px', fontSize: '0.75rem' }}
          onClick={onCreateConversation}
          title="Start a new conversation"
          id="sidebarNewConvBtn"
        >
          ➕ New
        </button>
      </div>
      <ConversationList
        conversations={conversations}
        activeConversationId={activeConversation?.id}
        onSelectConversation={onSelectConversation}
        onDeleteConversation={onDeleteConversation}
      />
    </div>
  );
};
