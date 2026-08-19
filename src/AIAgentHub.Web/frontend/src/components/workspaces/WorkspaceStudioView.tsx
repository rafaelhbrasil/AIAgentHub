import React, { useState, useEffect, useCallback } from 'react';
import { WorkspaceDto, FileTreeNode } from '../../types/workspace';
import { ConversationDto, ConversationDetailDto, MessageRole } from '../../types/conversation';
import { ModelInfo, ProviderStatusDto, ProviderStatus } from '../../types/provider';
import { FilePreviewDto, FileChangeDto } from '../../types/diff';
import { apiFetch } from '../../services/apiClient';
import { signalRService, StreamChunkPayload } from '../../services/signalrService';
import { useToast } from '../../context/ToastContext';
import { useModal } from '../../context/ModalContext';
import { FileTree } from './FileTree';
import { ConversationList } from './ConversationList';
import { ChatMessageList } from './ChatMessageList';
import { ChatInputBar } from './ChatInputBar';
import { ChangesOverviewBar } from './ChangesOverviewBar';
import { DiffViewerModal } from '../modals/DiffViewerModal';
import { FilePreviewModal } from '../modals/FilePreviewModal';
import { PermissionModal } from '../modals/PermissionModal';
import { CreateConversationModal } from '../modals/CreateConversationModal';
import { Spinner } from '../common/Spinner';

interface WorkspaceStudioViewProps {
  workspace: WorkspaceDto;
  initialConversationId?: string | null;
  onConversationChanged?: (conversationId: string | null) => void;
  onBack: () => void;
  onRemoveWorkspace?: (workspace: WorkspaceDto) => void;
}

export const WorkspaceStudioView: React.FC<WorkspaceStudioViewProps> = ({
  workspace,
  initialConversationId,
  onConversationChanged,
  onBack,
}) => {
  const { showToast } = useToast();
  const { showModal, hideModal } = useModal();

  const [treeData, setTreeData] = useState<FileTreeNode | null>(null);
  const [conversations, setConversations] = useState<ConversationDto[]>([]);
  const [activeConversation, setActiveConversation] = useState<ConversationDetailDto | null>(null);
  const [fileChanges, setFileChanges] = useState<FileChangeDto[]>([]);
  const [isChangesOverviewOpen, setIsChangesOverviewOpen] = useState<boolean>(false);
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [streamingContent, setStreamingContent] = useState<string>('');
  const [isStreaming, setIsStreaming] = useState<boolean>(false);
  const [isRefreshingTree, setIsRefreshingTree] = useState<boolean>(false);
  const [mobileTab, setMobileTab] = useState<'chat' | 'sidebar'>('chat');
  const [showActionsMenu, setShowActionsMenu] = useState<boolean>(false);

  const fetchFileTree = useCallback(async () => {
    setIsRefreshingTree(true);
    try {
      const treeRes = await apiFetch<FileTreeNode>(`/api/v1/filesystem/tree?workspaceId=${workspace.id}`);
      if (treeRes.ok && treeRes.data) {
        setTreeData(treeRes.data);
      }
    } finally {
      setIsRefreshingTree(false);
    }
  }, [workspace.id]);

  const fetchFileChanges = useCallback(async (convId: string) => {
    try {
      const res = await apiFetch<FileChangeDto[]>(`/api/v1/diffs?conversationId=${convId}&pendingOnly=true`);
      if (res.ok && res.data) {
        setFileChanges(res.data);
      } else {
        setFileChanges([]);
      }
    } catch {
      setFileChanges([]);
    }
  }, []);

  const selectConversationById = async (convId: string, notifyUrl: boolean = true) => {
    const res = await apiFetch<ConversationDetailDto>(`/api/v1/conversations/${convId}`);
    if (res.ok && res.data) {
      setActiveConversation(res.data);
      signalRService.joinConversation(convId);
      if (res.data.providerId) {
        loadModelsForProvider(res.data.providerId);
      }
      fetchFileChanges(convId);
      if (notifyUrl) {
        onConversationChanged?.(convId);
      }
    }
  };

  const fetchWorkspaceData = useCallback(async () => {
    try {
      const [treeRes, convsRes] = await Promise.all([
        apiFetch<FileTreeNode>(`/api/v1/filesystem/tree?workspaceId=${workspace.id}`),
        apiFetch<ConversationDto[]>(`/api/v1/conversations?workspaceId=${workspace.id}`),
      ]);

      if (treeRes.ok && treeRes.data) setTreeData(treeRes.data);
      const loadedConvs = convsRes.ok && convsRes.data ? convsRes.data : [];
      setConversations(loadedConvs);

      if (loadedConvs.length > 0) {
        const targetId =
          initialConversationId && loadedConvs.some((c) => c.id === initialConversationId)
            ? initialConversationId
            : loadedConvs[0].id;
        await selectConversationById(targetId);
      } else {
        setActiveConversation(null);
        setFileChanges([]);
        onConversationChanged?.(null);
      }
    } catch {
      // ignore
    }
  }, [workspace.id, initialConversationId]);

  const loadModelsForProvider = async (providerId: string) => {
    const res = await apiFetch<ModelInfo[]>(`/api/v1/providers/${providerId}/models`);
    if (res.ok && res.data) {
      setModels(res.data.filter((m) => m.isDisplayed !== false));
    }
  };

  useEffect(() => {
    fetchWorkspaceData();
  }, [fetchWorkspaceData]);

  // Re-join conversation and fetch latest state when page becomes visible again (e.g. Chrome restore on mobile)
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible' && activeConversation) {
        signalRService.joinConversation(activeConversation.id);
        selectConversationById(activeConversation.id);
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, [activeConversation]);

  // SignalR Event Listeners
  useEffect(() => {
    const handleStreamChunk = (payload: StreamChunkPayload) => {
      if (
        !activeConversation ||
        !payload.conversationId ||
        payload.conversationId.toLowerCase() === activeConversation.id.toLowerCase()
      ) {
        setIsStreaming(true);
        setStreamingContent((prev) => prev + payload.chunk);
      }
    };

    const handleConversationEvent = (data: any) => {
      if (data.eventName === 'conversation.completed' || data.eventName === 'conversation.aborted') {
        setIsStreaming(false);
        setStreamingContent('');
        if (data.eventName === 'conversation.aborted') {
          showToast('AI response cancelled.', 'info');
        } else {
          showToast('AI response completed.', 'success');
        }
        if (data.conversationId) {
          selectConversationById(data.conversationId);
          fetchFileChanges(data.conversationId);
        } else if (activeConversation) {
          fetchFileChanges(activeConversation.id);
        }
        fetchFileTree();
      }
    };

    const handlePermissionRequested = (req: any) => {
      showModal(
        '⚠️ Permission Required for AI Action',
        <PermissionModal request={req} onClose={hideModal} />
      );
    };

    const handleDiffCreated = (diff: any) => {
      const convId = diff.conversationId || diff.ConversationId;
      const path = diff.relativePath || diff.RelativePath;
      showToast(`File modified: ${path}`, 'info');
      if (activeConversation && activeConversation.id === convId) {
        selectConversationById(convId);
      }
      fetchFileTree();
      if (convId) {
        fetchFileChanges(convId);
      }
    };

    signalRService.onStreamChunk = handleStreamChunk;
    signalRService.onConversationEvent = handleConversationEvent;
    signalRService.onPermissionRequested = handlePermissionRequested;
    signalRService.onDiffCreated = handleDiffCreated;
    signalRService.onReconnected = () => {
      if (activeConversation) {
        selectConversationById(activeConversation.id);
      }
    };

    return () => {
      signalRService.onStreamChunk = undefined;
      signalRService.onConversationEvent = undefined;
      signalRService.onPermissionRequested = undefined;
      signalRService.onDiffCreated = undefined;
      signalRService.onReconnected = undefined;
    };
  }, [activeConversation, fetchFileTree, fetchFileChanges]);

  const handleCreateConversation = () => {
    showModal(
      'Start New Conversation',
      <CreateConversationModal
        defaultProviderId={workspace.settings?.defaultProviderId || 'antigravity'}
        defaultModelId={workspace.settings?.defaultModelId}
        onSubmit={async (title, providerId, modelId) => {
          const res = await apiFetch<ConversationDto>('/api/v1/conversations', {
            method: 'POST',
            body: {
              workspaceId: workspace.id,
              title: title.trim(),
              providerId: providerId || workspace.settings?.defaultProviderId || 'antigravity',
              modelId: modelId || undefined,
            },
          });

          if (res.ok && res.data) {
            hideModal();
            showToast('Conversation created.', 'success');
            setConversations((prev) => [res.data!, ...prev]);
            await selectConversationById(res.data.id);
            setMobileTab('chat');
          } else {
            showToast(res.error || (res.data as any)?.message || 'Failed to create conversation.', 'error');
          }
        }}
        onCancel={hideModal}
      />
    );
  };

  const handleDeleteConversation = (id: string, title: string) => {
    showModal(
      'Delete Conversation',
      <div>
        <p>
          Are you sure you want to delete the conversation <strong>"{title || 'Conversation'}"</strong>?
        </p>
        <div
          style={{
            background: 'rgba(239, 68, 68, 0.1)',
            border: '1px solid rgba(239, 68, 68, 0.3)',
            borderRadius: '6px',
            padding: '12px',
            marginTop: '14px',
            fontSize: '0.88rem',
            color: '#fca5a5',
          }}
        >
          ⚠️ <strong>Warning:</strong> All messages, streaming logs, and execution history for this
          conversation will be permanently deleted.
        </div>
      </div>,
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button type="button" className="btn btn-secondary" onClick={hideModal}>
          Cancel
        </button>
        <button
          type="button"
          className="btn btn-danger"
          onClick={async () => {
            const res = await apiFetch(`/api/v1/conversations/${id}`, { method: 'DELETE' });
            hideModal();
            if (res.ok || res.status === 204) {
              showToast(`Conversation "${title || 'Conversation'}" deleted.`, 'success');
              const remaining = conversations.filter((c) => c.id !== id);
              setConversations(remaining);
              if (activeConversation?.id === id) {
                if (remaining.length > 0) {
                  selectConversationById(remaining[0].id);
                } else {
                  setActiveConversation(null);
                  onConversationChanged?.(null);
                }
              }
            } else {
              showToast('Failed to delete conversation.', 'error');
            }
          }}
        >
          Delete Conversation
        </button>
      </div>
    );
  };

  const handlePreviewFile = async (relPath: string) => {
    const res = await apiFetch<FilePreviewDto>(
      `/api/v1/preview?workspaceId=${workspace.id}&path=${encodeURIComponent(relPath)}`
    );
    if (res.ok && res.data) {
      showModal(
        `Preview: ${relPath}`,
        <FilePreviewModal
          relativePath={relPath}
          preview={res.data}
          onClose={hideModal}
        />,
        undefined,
        'xl'
      );
    } else {
      showToast('Failed to preview file.', 'error');
    }
  };

  const handleOpenDiffs = (initialFileChangeId?: string) => {
    if (!activeConversation) return;
    showModal(
      'Changed Files & Diff Reviewer',
      <DiffViewerModal
        conversationId={activeConversation.id}
        workspaceId={workspace.id}
        initialFileChangeId={initialFileChangeId}
        onClose={hideModal}
        onRefreshWorkspace={() => {
          fetchWorkspaceData();
          fetchFileChanges(activeConversation.id);
        }}
      />,
      undefined,
      'xl'
    );
  };

  const handleAcceptAllChanges = async () => {
    if (!activeConversation) return;
    const res = await apiFetch(`/api/v1/diffs/accept-all?conversationId=${activeConversation.id}`, {
      method: 'POST',
    });
    if (res.ok) {
      showToast('All changes accepted.', 'success');
      fetchFileChanges(activeConversation.id);
      setIsChangesOverviewOpen(false);
    } else {
      showToast('Failed to accept changes.', 'error');
    }
  };

  const handleRejectAllChanges = async () => {
    if (!activeConversation) return;
    const res = await apiFetch(`/api/v1/diffs/reject-all?conversationId=${activeConversation.id}&workspaceId=${workspace.id}`, {
      method: 'POST',
    });
    if (res.ok) {
      showToast('All changes rejected and rolled back.', 'success');
      fetchFileChanges(activeConversation.id);
      fetchFileTree();
      setIsChangesOverviewOpen(false);
    } else {
      showToast('Failed to rollback changes.', 'error');
    }
  };

  const handleModelChange = async (modelId: string) => {
    if (!activeConversation) return;
    const cleanModelId = modelId ? modelId.trim() : null;
    const res = await apiFetch(`/api/v1/conversations/${activeConversation.id}/model`, {
      method: 'PUT',
      body: {
        modelId: cleanModelId,
        providerId: activeConversation.providerId,
        effort: activeConversation.effort,
      },
    });
    if (res.ok) {
      setActiveConversation((prev) => (prev ? { ...prev, modelId: cleanModelId || undefined } : prev));
      showToast(`Active model set to: ${cleanModelId || 'Default Model'}`, 'success');
    }
  };

  const handleEffortChange = async (effort: string) => {
    if (!activeConversation) return;
    const res = await apiFetch(`/api/v1/conversations/${activeConversation.id}/model`, {
      method: 'PUT',
      body: {
        modelId: activeConversation.modelId,
        providerId: activeConversation.providerId,
        effort,
      },
    });
    if (res.ok) {
      setActiveConversation((prev) => (prev ? { ...prev, effort } : prev));
      showToast(`Reasoning effort set to: ${effort || 'Default Effort'}`, 'success');
    }
  };

  const handleAbort = async () => {
    if (!activeConversation) return;
    try {
      await apiFetch(`/api/v1/conversations/${activeConversation.id}/abort`, { method: 'POST' });
      showToast('AI response cancelled by user.', 'info');
      
      setIsStreaming(false);
      const currentStream = (streamingContent || '').trim();
      const cancellationText = currentStream
        ? `${currentStream}\n\n*(AI response was cancelled by the user.)*`
        : '*(AI response was cancelled by the user.)*';

      setActiveConversation((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          messages: [
            ...prev.messages,
            {
              id: 'abort-' + Date.now(),
              conversationId: activeConversation.id,
              role: 'Assistant',
              content: cancellationText,
              createdAtUtc: new Date().toISOString(),
              metadata: { providerId: activeConversation.providerId },
            },
          ],
        };
      });
      setStreamingContent('');
    } catch {
      showToast('Failed to abort execution.', 'error');
    }
  };

  const handleSendPrompt = async (prompt: string) => {
    if (!activeConversation) return;

    // Check provider status before sending
    const statusRes = await apiFetch<ProviderStatusDto>(`/api/v1/providers/${activeConversation.providerId}/status`);
    if (statusRes.ok && statusRes.data) {
      const status = statusRes.data;
      if (status.status === ProviderStatus.QuotaExceeded) {
        let msg = 'Provider quota exceeded.';
        if (status.quotaResetsAt) {
          msg += ` Resets at ${new Date(status.quotaResetsAt).toLocaleString()}.`;
        }
        showToast(msg, 'error');
        return;
      }
      if (status.status === ProviderStatus.Unauthenticated) {
        showToast('Provider requires authentication. Please authenticate first.', 'error');
        return;
      }
    }

    // Append user message immediately to local state
    const userMsg = {
      id: Math.random().toString(),
      conversationId: activeConversation.id,
      role: MessageRole.User,
      content: prompt,
      createdAtUtc: new Date().toISOString(),
    };

    setActiveConversation((prev) =>
      prev ? { ...prev, messages: [...prev.messages, userMsg] } : prev
    );

    setIsStreaming(true);
    setStreamingContent('');

    await apiFetch(`/api/v1/conversations/${activeConversation.id}/prompt`, {
      method: 'POST',
      body: { prompt },
    });
  };

  return (
    <div className="studio-root">
      {/* Consolidated Compact Header */}
      <div className="studio-compact-header glass">
        <div className="studio-header-left">
          <button type="button" className="btn btn-secondary compact-btn" id="backToWsList" onClick={onBack} title="Back to Workspaces">
            &larr; <span className="hide-on-mobile">Workspaces</span>
          </button>
          
          <div className="studio-title-block">
            <span className="studio-ws-badge" title={workspace.path}>
              📁 {workspace.name}
            </span>
            {activeConversation && (
              <>
                <span className="studio-crumb-sep">/</span>
                <span
                  className="studio-conv-title"
                  title={`ID: ${activeConversation.id}\n${activeConversation.title}`}
                >
                  {activeConversation.title}
                </span>
                <span className="badge badge-provider">{activeConversation.providerId}</span>
              </>
            )}
          </div>
        </div>

        <div className="studio-header-right">
          {activeConversation && (
            <select
              id="convModelSelect"
              className="form-select compact-select"
              value={activeConversation.modelId || ''}
              onChange={(e) => handleModelChange(e.target.value)}
              title="Active Model"
            >
              <option value="">Default Model</option>
              {models
                .filter((m) => m.id && m.id.toLowerCase() !== 'default')
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.displayName || m.id}
                  </option>
                ))}
            </select>
          )}

          {/* Quick Actions Menu Trigger */}
          <div className="studio-actions-dropdown-wrap">
            <button
              type="button"
              className="btn btn-secondary compact-btn"
              id="optionsMenuBtn"
              onClick={() => setShowActionsMenu((prev) => !prev)}
              title="Workspace & Conversation Options"
            >
              ⚙️ <span className="hide-on-mobile">Options</span>
            </button>

            {showActionsMenu && (
              <>
                <div className="dropdown-backdrop" onClick={() => setShowActionsMenu(false)}></div>
                <div className="studio-actions-dropdown glass">
                  <button
                    type="button"
                    className="dropdown-item"
                    id="newConvBtn"
                    onClick={() => {
                      setShowActionsMenu(false);
                      handleCreateConversation();
                    }}
                  >
                    ➕ New Conversation
                  </button>

                  <button
                    type="button"
                    className="dropdown-item"
                    id="viewDiffsBtn"
                    onClick={() => {
                      setShowActionsMenu(false);
                      handleOpenDiffs();
                    }}
                    disabled={!activeConversation}
                  >
                    📝 Changed Files
                  </button>

                  {activeConversation && (
                    <>
                      <button
                        type="button"
                        className="dropdown-item"
                        id="copyConvLinkBtn"
                        onClick={() => {
                          setShowActionsMenu(false);
                          const url = `${window.location.origin}/workspaces/${workspace.id}/conversations/${activeConversation.id}`;
                          navigator.clipboard.writeText(url);
                          showToast('Conversation link copied to clipboard!', 'success');
                        }}
                      >
                        🔗 Copy Conversation Link
                      </button>

                      <button
                        type="button"
                        className="dropdown-item"
                        id="copyConvIdBtn"
                        onClick={() => {
                          setShowActionsMenu(false);
                          navigator.clipboard.writeText(activeConversation.id);
                          showToast('Conversation ID copied to clipboard!', 'success');
                        }}
                      >
                        📋 Copy Conversation ID
                      </button>
                    </>
                  )}

                  {activeConversation && (
                    <div className="dropdown-item-group">
                      <label style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: '4px' }}>Reasoning Effort:</label>
                      <select
                        id="convEffortSelect"
                        className="form-select compact-select"
                        value={activeConversation.effort || ''}
                        onChange={(e) => {
                          handleEffortChange(e.target.value);
                          setShowActionsMenu(false);
                        }}
                      >
                        <option value="">Default Effort</option>
                        <option value="low">Low Effort</option>
                        <option value="medium">Medium Effort</option>
                        <option value="high">High Effort</option>
                        <option value="max">Max Effort</option>
                      </select>
                    </div>
                  )}

                  <div style={{ borderTop: '1px solid var(--border-color)', margin: '4px 0' }}></div>

                  {activeConversation && (
                    <button
                      type="button"
                      className="dropdown-item text-danger"
                      id="deleteCurrentConvBtn"
                      onClick={() => {
                        setShowActionsMenu(false);
                        handleDeleteConversation(activeConversation.id, activeConversation.title);
                      }}
                    >
                      🗑️ Delete Current Conversation
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Mobile Switcher Tab (Chats vs Files) */}
      <div className="studio-mobile-nav">
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'chat' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center' }}
          onClick={() => setMobileTab('chat')}
        >
          💬 Chat Studio
        </button>
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'sidebar' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center' }}
          onClick={() => setMobileTab('sidebar')}
        >
          📁 Files & Chats ({conversations.length})
        </button>
      </div>

      <div className="studio-layout">
        {/* Left Panel: Explorer & Conversations */}
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
            <button
              type="button"
              className="btn-refresh-icon"
              onClick={fetchFileTree}
              disabled={isRefreshingTree}
              title="Refresh Files & Folders structure"
              id="refreshFilesBtn"
            >
              <Spinner size={16} isSpinning={isRefreshingTree} />
            </button>
          </div>
          <FileTree node={treeData} onSelectFile={handlePreviewFile} />

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
              onClick={handleCreateConversation}
              title="Start a new conversation"
              id="sidebarNewConvBtn"
            >
              ➕ New
            </button>
          </div>
          <ConversationList
            conversations={conversations}
            activeConversationId={activeConversation?.id}
            onSelectConversation={(id) => {
              selectConversationById(id);
              setMobileTab('chat');
            }}
            onDeleteConversation={handleDeleteConversation}
          />
        </div>

        {/* Right Panel: Conversation Studio */}
        <div className={`chat-container glass ${mobileTab === 'sidebar' ? 'mobile-hidden' : ''}`} id="chatPanel">
          {!activeConversation ? (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                height: '100%',
                color: 'var(--text-muted)',
                padding: '40px',
                textAlign: 'center',
              }}
            >
              <div style={{ fontSize: '3rem', marginBottom: '12px', opacity: 0.5 }}>💬</div>
              <h3 style={{ marginBottom: '8px', color: 'var(--text-heading)' }}>
                No Active Conversation
              </h3>
              <p style={{ fontSize: '0.9rem', maxWidth: '400px', marginBottom: '16px' }}>
                Create a new conversation or select one from the sidebar to begin pair programming.
              </p>
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleCreateConversation}
              >
                + Start New Conversation
              </button>
            </div>
          ) : (
            <>
              <ChatMessageList
                messages={activeConversation.messages || []}
                providerId={activeConversation.providerId}
                streamingContent={streamingContent}
                isStreaming={isStreaming}
              />

              <div className="chat-bottom-dock">
                <ChangesOverviewBar
                  fileChanges={fileChanges}
                  isOpen={isChangesOverviewOpen}
                  onToggleOpen={() => setIsChangesOverviewOpen((prev) => !prev)}
                  onSelectFile={(fileChangeId) => handleOpenDiffs(fileChangeId)}
                  onOpenFullDiff={() => handleOpenDiffs()}
                  onAcceptAll={handleAcceptAllChanges}
                  onRejectAll={handleRejectAllChanges}
                />

                <ChatInputBar
                  onSend={handleSendPrompt}
                  disabled={isStreaming}
                  isStreaming={isStreaming}
                  onAbort={handleAbort}
                />
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
