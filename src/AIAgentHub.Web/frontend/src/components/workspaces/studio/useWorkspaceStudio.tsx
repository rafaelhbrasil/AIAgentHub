import { useState, useEffect, useCallback, useRef } from 'react';
import { WorkspaceDto, FileTreeNode } from '../../../types/workspace';
import { ConversationDto, ConversationDetailDto, MessageRole } from '../../../types/conversation';
import { ModelInfo, ProviderStatusDto, ProviderStatus } from '../../../types/provider';
import { FilePreviewDto, FileChangeDto } from '../../../types/diff';
import { apiFetch } from '../../../services/apiClient';
import { signalRService, StreamChunkPayload } from '../../../services/signalrService';
import { useToast } from '../../../context/ToastContext';
import { useModal } from '../../../context/ModalContext';
import { CreateConversationModal } from '../../modals/CreateConversationModal';
import { SwitchProviderModal } from '../../modals/SwitchProviderModal';
import { AbortMigrationModal } from '../../modals/AbortMigrationModal';
import { FilePreviewModal } from '../../modals/FilePreviewModal';
import { DiffViewerModal } from '../../modals/DiffViewerModal';
import { PermissionModal } from '../../modals/PermissionModal';

export interface UseWorkspaceStudioProps {
  workspace: WorkspaceDto;
  initialConversationId?: string | null;
  onConversationChanged?: (conversationId: string | null) => void;
  onRemoveWorkspace?: (workspace: WorkspaceDto) => void;
}

export const useWorkspaceStudio = ({
  workspace,
  initialConversationId,
  onConversationChanged,
}: UseWorkspaceStudioProps) => {
  const { showToast } = useToast();
  const { showModal, hideModal } = useModal();

  const [treeData, setTreeData] = useState<FileTreeNode | null>(null);
  const [conversations, setConversations] = useState<ConversationDto[]>([]);
  const [activeConversation, setActiveConversation] = useState<ConversationDetailDto | null>(null);
  const [fileChanges, setFileChanges] = useState<FileChangeDto[]>([]);
  const [isChangesOverviewOpen, setIsChangesOverviewOpen] = useState<boolean>(false);
  const [models, setModels] = useState<ModelInfo[]>([]);
  const [streamingContent, setStreamingContent] = useState<string>('');
  const [heartbeatMessages, setHeartbeatMessages] = useState<string[]>([]);
  const [isStreaming, setIsStreaming] = useState<boolean>(false);
  const [isRefreshingTree, setIsRefreshingTree] = useState<boolean>(false);
  const [mobileTab, setMobileTab] = useState<'chat' | 'sidebar'>('chat');
  const [showActionsMenu, setShowActionsMenu] = useState<boolean>(false);

  const onConversationChangedRef = useRef(onConversationChanged);
  useEffect(() => {
    onConversationChangedRef.current = onConversationChanged;
  }, [onConversationChanged]);

  const activeConversationRef = useRef(activeConversation);
  useEffect(() => {
    activeConversationRef.current = activeConversation;
  }, [activeConversation]);

  const initialConversationIdRef = useRef(initialConversationId);

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

  const loadModelsForProvider = useCallback(async (providerId: string) => {
    const res = await apiFetch<ModelInfo[]>(`/api/v1/providers/${providerId}/models`);
    if (res.ok && res.data) {
      setModels(res.data.filter((m) => m.isDisplayed !== false));
    }
  }, []);

  const selectConversationById = useCallback(
    async (convId: string, notifyUrl: boolean = true) => {
      const res = await apiFetch<ConversationDetailDto>(`/api/v1/conversations/${convId}`);
      if (res.ok && res.data) {
        setActiveConversation(res.data);
        signalRService.joinConversation(convId);
        if (res.data.providerId) {
          loadModelsForProvider(res.data.providerId);
        }
        fetchFileChanges(convId);
        if (notifyUrl) {
          onConversationChangedRef.current?.(convId);
        }
      }
    },
    [fetchFileChanges, loadModelsForProvider]
  );

  const fetchWorkspaceData = useCallback(async () => {
    try {
      const [treeRes, convsRes] = await Promise.all([
        apiFetch<FileTreeNode>(`/api/v1/filesystem/tree?workspaceId=${workspace.id}`),
        apiFetch<ConversationDto[]>(`/api/v1/conversations?workspaceId=${workspace.id}`),
      ]);

      if (treeRes.ok && treeRes.data) setTreeData(treeRes.data);
      const loadedConvs = convsRes.ok && convsRes.data ? convsRes.data : [];
      const sortedConvs = [...loadedConvs].sort((a, b) => {
        const timeA = new Date(a.lastUserInteractionAtUtc || a.updatedAtUtc || a.createdAtUtc).getTime();
        const timeB = new Date(b.lastUserInteractionAtUtc || b.updatedAtUtc || b.createdAtUtc).getTime();
        return timeB - timeA;
      });
      setConversations(sortedConvs);

      if (sortedConvs.length > 0) {
        const initialTarget = initialConversationIdRef.current;
        const targetId =
          initialTarget && loadedConvs.some((c) => c.id === initialTarget)
            ? initialTarget
            : sortedConvs[0].id;
        await selectConversationById(targetId);
      } else {
        setActiveConversation(null);
        setFileChanges([]);
        onConversationChangedRef.current?.(null);
      }
    } catch {
      // ignore
    }
  }, [workspace.id, selectConversationById]);

  useEffect(() => {
    fetchWorkspaceData();
  }, [fetchWorkspaceData]);

  // Handle external navigation change (e.g. browser back/forward buttons)
  useEffect(() => {
    if (initialConversationId && initialConversationId !== activeConversationRef.current?.id) {
      selectConversationById(initialConversationId, false);
    }
  }, [initialConversationId, selectConversationById]);

  // Re-join conversation and fetch latest state when page becomes visible again (e.g. Chrome restore on mobile)
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible' && activeConversationRef.current) {
        signalRService.joinConversation(activeConversationRef.current.id);
        selectConversationById(activeConversationRef.current.id, false);
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, [selectConversationById]);

  // SignalR Event Listeners
  useEffect(() => {
    const handleStreamChunk = (payload: StreamChunkPayload) => {
      if (
        !activeConversationRef.current ||
        !payload.conversationId ||
        payload.conversationId.toLowerCase() === activeConversationRef.current.id.toLowerCase()
      ) {
        setIsStreaming(true);
        setHeartbeatMessages([]);
        setStreamingContent((prev) => prev + payload.chunk);
      }
    };

    const handleConversationEvent = (data: any) => {
      if (data.eventName === 'conversation.heartbeat') {
        if (
          !activeConversationRef.current ||
          !data.conversationId ||
          data.conversationId.toLowerCase() === activeConversationRef.current.id.toLowerCase()
        ) {
          setIsStreaming(true);
          const msg = data.data?.message || data.data?.Message || 'Still thinking...';
          setHeartbeatMessages((prev) => [...prev, msg]);
        }
        return;
      }

      if (data.eventName === 'conversation.completed' || data.eventName === 'conversation.aborted') {
        setIsStreaming(false);
        setStreamingContent('');
        setHeartbeatMessages([]);
        if (data.eventName === 'conversation.aborted') {
          showToast('AI response cancelled.', 'info');
        } else {
          showToast('AI response completed.', 'success');
        }
        if (data.conversationId) {
          selectConversationById(data.conversationId);
          fetchFileChanges(data.conversationId);
        } else if (activeConversationRef.current) {
          fetchFileChanges(activeConversationRef.current.id);
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
      if (activeConversationRef.current && activeConversationRef.current.id === convId) {
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
      if (activeConversationRef.current) {
        selectConversationById(activeConversationRef.current.id, false);
      }
    };

    return () => {
      signalRService.onStreamChunk = undefined;
      signalRService.onConversationEvent = undefined;
      signalRService.onPermissionRequested = undefined;
      signalRService.onDiffCreated = undefined;
      signalRService.onReconnected = undefined;
    };
  }, [fetchFileTree, fetchFileChanges, selectConversationById, showModal, hideModal, showToast]);

  const handleCreateConversation = () => {
    showModal(
      'Start New Conversation',
      <CreateConversationModal
        defaultProviderId={workspace.settings?.defaultProviderId || ''}
        defaultModelId={workspace.settings?.defaultModelId}
        onSubmit={async (title, providerId, modelId) => {
          const res = await apiFetch<ConversationDto>('/api/v1/conversations', {
            method: 'POST',
            body: {
              workspaceId: workspace.id,
              title: title.trim(),
              providerId: providerId || workspace.settings?.defaultProviderId || undefined,
              modelId: modelId || undefined,
            },
          });

          if (res.ok && res.data) {
            hideModal();
            showToast('Conversation created.', 'success');
            setConversations((prev) => [res.data!, ...prev.filter((c) => c.id !== res.data!.id)]);
            await selectConversationById(res.data.id, true);
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
                  selectConversationById(remaining[0].id, true);
                } else {
                  setActiveConversation(null);
                  setFileChanges([]);
                  onConversationChangedRef.current?.(null);
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

  const handleTogglePin = async (convId: string, isPinned: boolean) => {
    const res = await apiFetch(`/api/v1/conversations/${convId}/pin`, {
      method: 'PUT',
      body: { isPinned },
    });
    if (res.ok) {
      setConversations((prev) =>
        prev.map((c) => (c.id === convId ? { ...c, isPinned } : c))
      );
      if (activeConversation && activeConversation.id === convId) {
        setActiveConversation((prev) => (prev ? { ...prev, isPinned } : prev));
      }
    } else {
      showToast('Failed to update pin state.', 'error');
    }
  };

  const handleOpenSwitchProvider = () => {
    if (!activeConversation) return;

    if (isStreaming) {
      showToast('Cannot switch provider while command is running. Please wait for it to finish or abort it.', 'warning');
      return;
    }

    const isSwitching =
      activeConversation.status === 1 ||
      (activeConversation.status as any) === 'SwitchingProvider';

    if (isSwitching) {
      showModal(
        'Provider Migration in Progress',
        <AbortMigrationModal
          conversation={activeConversation}
          onSuccess={async (updatedConv) => {
            hideModal();
            if (updatedConv) {
              setActiveConversation(updatedConv);
            } else {
              await selectConversationById(activeConversation.id);
            }
          }}
          onClose={hideModal}
        />,
        undefined,
        'md'
      );
      return;
    }

    showModal(
      '🔄 Switch AI Provider & History Replay',
      <SwitchProviderModal
        conversation={activeConversation}
        onSuccess={async (result) => {
          hideModal();
          await selectConversationById(result.conversationId);
          fetchWorkspaceData();
        }}
        onCancel={hideModal}
      />,
      undefined,
      'lg'
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
      'full'
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
    const res = await apiFetch(
      `/api/v1/diffs/reject-all?conversationId=${activeConversation.id}&workspaceId=${workspace.id}`,
      { method: 'POST' }
    );
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
      setHeartbeatMessages([]);
    } catch {
      showToast('Failed to abort execution.', 'error');
    }
  };

  const handleSendPrompt = async (prompt: string) => {
    if (!activeConversation) return;

    // Check provider status before sending
    const statusRes = await apiFetch<ProviderStatusDto>(
      `/api/v1/providers/${activeConversation.providerId}/status`
    );
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

    // Append user message immediately to local state and bump recency
    const now = new Date().toISOString();
    const userMsg = {
      id: Math.random().toString(),
      conversationId: activeConversation.id,
      role: MessageRole.User,
      content: prompt,
      createdAtUtc: now,
    };

    setActiveConversation((prev) =>
      prev
        ? {
            ...prev,
            messages: [...prev.messages, userMsg],
            updatedAtUtc: now,
            lastUserInteractionAtUtc: now,
          }
        : prev
    );

    setConversations((prev) => {
      const active = prev.find((c) => c.id === activeConversation.id);
      if (!active) return prev;
      const updatedActive: ConversationDto = {
        ...active,
        updatedAtUtc: now,
        lastUserInteractionAtUtc: now,
        messageCount: (active.messageCount || 0) + 1,
      };
      const others = prev.filter((c) => c.id !== activeConversation.id);
      return [updatedActive, ...others];
    });

    setIsStreaming(true);
    setStreamingContent('');
    setHeartbeatMessages([]);

    await apiFetch(`/api/v1/conversations/${activeConversation.id}/prompt`, {
      method: 'POST',
      body: { prompt },
    });
  };

  const handleDownloadZip = async () => {
    showToast('Preparing project ZIP archive download...', 'info');
    try {
      const res = await fetch(`/api/v1/workspaces/${workspace.id}/download`, { method: 'GET' });
      if (!res.ok) {
        showToast('Failed to download project ZIP archive.', 'error');
        return;
      }

      const skippedHeader = res.headers.get('X-Skipped-Files');
      if (skippedHeader) {
        try {
          const skippedFiles: string[] = JSON.parse(skippedHeader);
          if (Array.isArray(skippedFiles) && skippedFiles.length > 0) {
            console.warn(
              `[ZIP Export] Workspace ${workspace.id} skipped ${skippedFiles.length} inaccessible files:`,
              skippedFiles
            );
            showModal(
              '⚠️ Inaccessible Files Skipped',
              <div>
                <p style={{ marginBottom: '12px', color: 'var(--text-muted)', fontSize: '0.88rem' }}>
                  The project ZIP was downloaded, but the following <strong>{skippedFiles.length}</strong> file(s)
                  could not be read (e.g. locked or restricted permissions):
                </p>
                <div
                  style={{
                    maxHeight: '200px',
                    overflowY: 'auto',
                    background: 'rgba(0, 0, 0, 0.4)',
                    padding: '8px 12px',
                    borderRadius: '4px',
                    fontFamily: 'var(--font-mono)',
                    fontSize: '0.78rem',
                    border: '1px solid var(--border-color)',
                  }}
                >
                  {skippedFiles.map((file, idx) => (
                    <div key={idx} style={{ color: '#fca5a5', padding: '2px 0' }}>
                      ⚠️ {file}
                    </div>
                  ))}
                </div>
              </div>,
              <button type="button" className="btn btn-primary" onClick={hideModal}>
                Understood
              </button>
            );
          }
        } catch {
          // Ignore header parse error
        }
      }

      const blob = await res.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${workspace.name || 'project'}.zip`;
      document.body.appendChild(link);
      link.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(link);
      showToast('Project ZIP downloaded successfully.', 'success');
    } catch (err: any) {
      showToast(err.message || 'Error downloading project ZIP.', 'error');
    }
  };

  return {
    treeData,
    conversations,
    activeConversation,
    fileChanges,
    isChangesOverviewOpen,
    setIsChangesOverviewOpen,
    models,
    streamingContent,
    heartbeatMessages,
    isStreaming,
    isRefreshingTree,
    mobileTab,
    setMobileTab,
    showActionsMenu,
    setShowActionsMenu,
    fetchFileTree,
    fetchFileChanges,
    selectConversationById,
    fetchWorkspaceData,
    handleCreateConversation,
    handleDeleteConversation,
    handleTogglePin,
    handleOpenSwitchProvider,
    handlePreviewFile,
    handleOpenDiffs,
    handleAcceptAllChanges,
    handleRejectAllChanges,
    handleModelChange,
    handleEffortChange,
    handleAbort,
    handleSendPrompt,
    handleDownloadZip,
  };
};
