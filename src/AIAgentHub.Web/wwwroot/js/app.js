// AI Agent Hub — Single Page Web Application ES6+
const state = {
  isSetupCompleted: false,
  isAuthenticated: false,
  canResetWithoutCode: false,
  isRecoveryModeEnabled: false,
  username: 'admin',
  activeTab: 'dashboard',
  workspaces: [],
  currentWorkspace: null,
  conversations: [],
  currentConversation: null,
  providers: [],
  providerModels: {},
  hubConnection: null,
  activeStreamingMessageId: null
};

// --- Loading Skeleton Renderers ---
function renderDashboardSkeletons() {
  return `
    <div class="grid-cols-3">
      <div class="skeleton-card">
        <div class="skeleton skeleton-line skeleton-line-medium"></div>
        <div class="skeleton skeleton-line skeleton-line-short"></div>
        <div class="skeleton skeleton-stat"></div>
      </div>
      <div class="skeleton-card">
        <div class="skeleton skeleton-line skeleton-line-medium"></div>
        <div class="skeleton skeleton-line skeleton-line-short"></div>
        <div class="skeleton skeleton-stat"></div>
      </div>
      <div class="skeleton-card">
        <div class="skeleton skeleton-line skeleton-line-medium"></div>
        <div class="skeleton skeleton-line skeleton-line-short"></div>
        <div class="skeleton skeleton-stat"></div>
      </div>
    </div>
    <div class="skeleton-card" style="margin-bottom: 24px;">
      <div class="skeleton skeleton-line skeleton-line-long"></div>
      <div style="margin-top: 16px; display: flex; flex-direction: column; gap: 10px;">
        ${Array(5).fill('').map(() => `
          <div style="display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; background: rgba(0,0,0,0.25); border-radius: 6px;">
            <div style="flex: 1;">
              <div class="skeleton skeleton-line skeleton-line-medium"></div>
              <div class="skeleton skeleton-line skeleton-line-short" style="margin-top: 6px;"></div>
            </div>
            <div class="skeleton skeleton-badge" style="margin-left: 12px;"></div>
          </div>
        `).join('')}
      </div>
    </div>
  `;
}

function renderProviderSkeletons() {
  return `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
      <h2>AI Providers</h2>
      <button class="btn btn-secondary" disabled>🔄 Refresh All Providers</button>
    </div>
    <div class="grid-cols-3">
      ${Array(4).fill('').map(() => `
        <div class="skeleton-card">
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <div class="skeleton skeleton-line skeleton-line-medium"></div>
            <div class="skeleton skeleton-badge"></div>
          </div>
          <div class="skeleton skeleton-line skeleton-line-short" style="margin-top: 8px;"></div>
          <div class="skeleton skeleton-line skeleton-line-long" style="margin-top: 12px;"></div>
          <div style="display: flex; gap: 8px; margin-top: 12px;">
            <div class="skeleton skeleton-badge"></div>
          </div>
        </div>
      `).join('')}
    </div>
  `;
}

// --- Loading Overlay ---
function showLoadingOverlay(text = 'Loading...') {
  let overlay = document.getElementById('loadingOverlay');
  if (!overlay) {
    overlay = document.createElement('div');
    overlay.id = 'loadingOverlay';
    overlay.className = 'loading-overlay';
    overlay.innerHTML = `
      <div class="loading-spinner"></div>
      <div class="loading-text">${text}</div>
    `;
    document.body.appendChild(overlay);
  } else {
    overlay.querySelector('.loading-text').textContent = text;
    overlay.classList.remove('hidden');
  }
}

function hideLoadingOverlay() {
  const overlay = document.getElementById('loadingOverlay');
  if (overlay) overlay.classList.add('hidden');
}

// --- Performance Benchmarks ---
const BENCHMARKS_ENABLED = true;

function benchmarkMark(name) {
  if (BENCHMARKS_ENABLED) performance.mark(name);
}

function benchmarkMeasure(name, startMark, endMark) {
  if (!BENCHMARKS_ENABLED) return;
  try {
    performance.measure(name, startMark, endMark);
    const entries = performance.getEntriesByName(name);
    if (entries.length > 0) {
      const duration = entries[entries.length - 1].duration.toFixed(2);
      console.log(`[Benchmark] ${name}: ${duration}ms`);
    }
  } catch {
    // Marks may not exist
  }
}

// --- Client-Side Router ---
function navigateTo(path, pushState = true) {
  if (pushState) {
    history.pushState({ path }, '', path);
  }
  handleRoute(path);
}

function handleRoute(path) {
  console.log('Handling route:', path);
  if (!state.isSetupCompleted) {
    showSetupWizardModal();
    return;
  }
  if (!state.isAuthenticated) {
    renderSignInPage();
    return;
  }

  const parts = path.split('/').filter(Boolean);
  
  if (parts.length === 0 || parts[0] === 'dashboard') {
    state.activeTab = 'dashboard';
    updateNavActiveState('dashboard');
    renderActiveTab();
  } else if (parts[0] === 'workspaces') {
    if (parts.length === 1) {
      state.activeTab = 'workspaces';
      state.currentWorkspace = null;
      updateNavActiveState('workspaces');
      renderActiveTab();
    } else if (parts.length >= 2) {
      const workspaceId = parts[1];
      openWorkspaceById(workspaceId);
    }
  } else if (parts[0] === 'providers') {
    state.activeTab = 'providers';
    updateNavActiveState('providers');
    renderActiveTab();
  } else if (parts[0] === 'tools' || parts[0] === 'mcps') {
    state.activeTab = 'tools';
    updateNavActiveState('tools');
    renderActiveTab();
  } else if (parts[0] === 'settings') {
    state.activeTab = 'settings';
    updateNavActiveState('settings');
    renderActiveTab();
  } else {
    console.warn('Unknown route:', path);
  }
}

function updateNavActiveState(tab) {
  console.log('Updating nav active state to:', tab);
  document.querySelectorAll('.nav-btn').forEach((btn) => {
    btn.classList.toggle('active', btn.dataset.tab === tab);
  });
}

async function openWorkspaceById(workspaceId) {
  const res = await apiFetch(`/api/v1/workspaces/${workspaceId}`);
  if (!res.ok || !res.data) {
    showToast('Workspace not found.', 'error');
    navigateTo('/workspaces', false);
    return;
  }
  
  state.currentWorkspace = res.data;
  state.activeTab = 'workspaces';
  updateNavActiveState('workspaces');
  
  const convRes = await apiFetch(`/api/v1/conversations?workspaceId=${workspaceId}`);
  state.conversations = (convRes.ok && convRes.data) ? convRes.data : [];
  
  if (state.conversations.length > 0) {
    state.currentConversation = state.conversations[0];
  }
  
  if (state.currentConversation && state.hubConnection) {
    state.hubConnection.invoke('JoinConversation', state.currentConversation.id).catch(() => {});
  }
  
  renderActiveTab();
}

// Handle browser back/forward
window.addEventListener('popstate', (event) => {
  const path = event.state?.path || window.location.pathname;
  handleRoute(path);
});



// --- Initialization ---
document.addEventListener('DOMContentLoaded', async () => {
  setupNavigation();
  await checkAuthAndSetup();
  initSignalR();
});

// --- API Client Helpers ---
async function apiFetch(url, options = {}) {
  options.headers = options.headers || {};
  if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData)) {
    options.headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(options.body);
  }
  options.credentials = 'include';

  try {
    const res = await fetch(url, options);
    if (res.status === 401 && !url.includes('/auth/')) {
      state.isAuthenticated = false;
      renderSignInPage();
      return { success: false, status: 401 };
    }
    const data = await res.json().catch(() => null);
    return { ok: res.ok, status: res.status, data };
  } catch (err) {
    showToast('Network error: ' + err.message, 'error');
    return { ok: false, error: err.message };
  }
}

function updateUserMenuUI() {
  const userMenu = document.getElementById('userMenu');
  if (!userMenu) return;
  if (state.isAuthenticated) {
    const userLabel = document.getElementById('userNameLabel');
    if (userLabel) userLabel.innerText = state.username || 'admin';
    userMenu.style.display = 'flex';
  } else {
    userMenu.style.display = 'none';
  }
}

// --- Auth & Setup Check ---
async function checkAuthAndSetup() {
  const setupRes = await apiFetch('/api/v1/auth/setup/status');
  if (setupRes.ok && setupRes.data) {
    state.isSetupCompleted = setupRes.data.isSetupCompleted;
    state.canResetWithoutCode = !!setupRes.data.canResetWithoutCode;
    state.isRecoveryModeEnabled = !!setupRes.data.isRecoveryModeEnabled;
  }

  if (!state.isSetupCompleted) {
    state.isAuthenticated = false;
    updateUserMenuUI();
    showSetupWizardModal();
    return;
  }

  const sessionRes = await apiFetch('/api/v1/auth/session');
  if (sessionRes.ok && sessionRes.data && sessionRes.data.isAuthenticated) {
    state.isAuthenticated = true;
    state.username = sessionRes.data.username || 'admin';
    updateUserMenuUI();
    hideModal();
    loadGlobalProviders();
    const initialPath = window.location.pathname;
    if (initialPath && initialPath !== '/') {
      handleRoute(initialPath);
    } else {
      renderActiveTab();
    }
  } else {
    state.isAuthenticated = false;
    updateUserMenuUI();
    renderSignInPage();
  }
}

// --- SignalR Real-Time Hub ---
function initSignalR() {
  if (typeof signalR === 'undefined') return;

  state.hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/agent')
    .withAutomaticReconnect()
    .build();

  state.hubConnection.on('streamChunk', (data) => {
    const convId = (data.conversationId || data.ConversationId || '').toString().toLowerCase();
    const currentId = (state.currentConversation?.id || '').toString().toLowerCase();
    const chunk = data.chunk || data.Chunk || '';
    if (!convId || !currentId || convId === currentId) {
      appendStreamChunk(chunk);
    }
  });

  state.hubConnection.on('conversationEvent', (data) => {
    const eventName = data.eventName || data.EventName;
    const convId = (data.conversationId || data.ConversationId || '').toString();
    if (eventName === 'conversation.completed') {
      clearStreamingState();
      showToast('AI response completed.', 'success');
      if (convId) loadConversationDetails(convId);
    }
  });

  state.hubConnection.on('permissionRequested', (req) => {
    showPermissionModal(req);
  });

  state.hubConnection.on('diffCreated', (diff) => {
    const convId = diff.conversationId || diff.ConversationId;
    const path = diff.relativePath || diff.RelativePath;
    showToast(`File modified: ${path}`, 'info');
    if (state.currentConversation && state.currentConversation.id === convId) {
      loadConversationDetails(convId);
    }
  });

  state.hubConnection.on('notification', (n) => {
    showToast(`${n.title}: ${n.message}`, n.level === 'error' ? 'error' : 'info');
  });

  state.hubConnection.start()
    .then(() => {
      if (state.currentConversation) {
        state.hubConnection.invoke('JoinConversation', state.currentConversation.id).catch(() => {});
      }
    })
    .catch((err) => console.log('SignalR connection error:', err));
}

let currentStreamingBubble = null;
let currentStreamingContent = '';

function appendStreamChunk(chunk) {
  const messageList = document.getElementById('messageList');
  if (!messageList) return;

  if (!currentStreamingBubble) {
    currentStreamingContent = '';
    const bubble = document.createElement('div');
    bubble.className = 'message-item message-assistant streaming-active';
    bubble.innerHTML = `
      <div class="message-header">
        <span>⚡ AI Assistant (${escapeHtml(state.currentConversation?.providerId || 'AI')})</span>
        <span style="margin-left: auto;">${new Date().toLocaleTimeString()}</span>
      </div>
      <div class="message-body markdown-rendered" id="activeStreamingBody"></div>
    `;
    const anchor = document.getElementById('streamingAnchor');
    if (anchor) {
      messageList.insertBefore(bubble, anchor);
    } else {
      messageList.appendChild(bubble);
    }
    currentStreamingBubble = bubble;
  }

  currentStreamingContent += chunk;
  const bodyEl = document.getElementById('activeStreamingBody');
  if (bodyEl) {
    bodyEl.innerHTML = formatMessageContent(currentStreamingContent);
  }
  messageList.scrollTop = messageList.scrollHeight;
}

function clearStreamingState() {
  if (currentStreamingBubble) {
    currentStreamingBubble.classList.remove('streaming-active');
  }
  currentStreamingBubble = null;
  currentStreamingContent = '';
}

// --- Navigation ---
function setupNavigation() {
  // Use event delegation on document body for more robust handling
  document.body.addEventListener('click', (e) => {
    const navBtn = e.target.closest('.nav-btn');
    if (navBtn) {
      e.preventDefault();
      if (!state.isAuthenticated) return;
      const tab = navBtn.dataset.tab;
      console.log('Navigation clicked:', tab);
      navigateTo(tab === 'dashboard' ? '/' : `/${tab}`);
    }
  });

  const modalCloseBtn = document.getElementById('modalCloseBtn');
  if (modalCloseBtn) {
    modalCloseBtn.addEventListener('click', hideModal);
  }

  const logoutBtn = document.getElementById('logoutBtn');
  if (logoutBtn) {
    logoutBtn.addEventListener('click', async () => {
      const res = await apiFetch('/api/v1/auth/logout', { method: 'POST' });
      if (res.ok) {
        showToast('Signed out successfully.', 'info');
      }
      state.isAuthenticated = false;
      updateUserMenuUI();
      renderSignInPage();
    });
  }
}

function renderActiveTab() {
  updateUserMenuUI();
  if (!state.isSetupCompleted) {
    showSetupWizardModal();
    return;
  }
  if (!state.isAuthenticated) {
    renderSignInPage();
    return;
  }
  console.log('Rendering active tab:', state.activeTab);
  const container = document.getElementById('mainContent');
  if (!container) {
    console.error('mainContent element not found!');
    return;
  }
  switch (state.activeTab) {
    case 'dashboard':
      renderDashboard(container);
      break;
    case 'workspaces':
      renderWorkspaces(container);
      break;
    case 'providers':
      renderProviders(container);
      break;
    case 'tools':
      renderTools(container);
      break;
    case 'settings':
      renderSettings(container);
      break;
    default:
      console.warn('Unknown activeTab:', state.activeTab);
  }
}

// --- Dashboard View ---
async function renderDashboard(container) {
  benchmarkMark('dashboard-render-start');
  
  // Show skeletons while loading
  container.innerHTML = renderDashboardSkeletons();

  // Fetch data from backend (backend handles caching)
  const [wsRes, provRes] = await Promise.all([
    apiFetch('/api/v1/workspaces'),
    apiFetch('/api/v1/providers')
  ]);

  state.workspaces = (wsRes.ok && wsRes.data) ? wsRes.data : [];
  state.providers = (provRes.ok && provRes.data) ? provRes.data : [];

  const installedCount = state.providers.filter((p) => p.isInstalled).length;

  benchmarkMark('dashboard-render-end');

  container.innerHTML = `
    <div class="grid-cols-3">
      <div class="card glass">
        <div class="card-title">Managed Workspaces <span>📁</span></div>
        <div class="card-subtitle">Active local projects</div>
        <div class="stat-val">${state.workspaces.length}</div>
      </div>
      <div class="card glass">
        <div class="card-title">Available Providers <span>⚡</span></div>
        <div class="card-subtitle">Antigravity, Gemini, Codex, Claude</div>
        <div class="stat-val">${installedCount} / ${state.providers.length}</div>
      </div>
      <div class="card glass">
        <div class="card-title">Security & Port <span>🔒</span></div>
        <div class="card-subtitle">HTTPS Self-Signed TLS</div>
        <div class="stat-val" style="font-size: 1.6rem; color: #34d399;">Port 5432</div>
      </div>
    </div>

    <div class="card glass" style="margin-bottom: 24px;">
      <div class="card-title">
        <span>Recent Workspaces</span>
        <button class="btn btn-primary" id="dashNewWsBtn">+ Open or Create Workspace</button>
      </div>
      <div style="margin-top: 16px;">
        ${
          state.workspaces.length === 0
            ? '<p class="card-subtitle">No workspaces opened yet. Click above to open a folder on the server.</p>'
            : `<div style="display: flex; flex-direction: column; gap: 10px;">
                ${state.workspaces.map((w) => `
                  <div style="display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; background: rgba(0,0,0,0.25); border-radius: 6px;">
                    <div>
                      <strong>${escapeHtml(w.name)}</strong>
                      <div style="font-size: 0.8rem; color: var(--text-muted);">${escapeHtml(w.path)}</div>
                    </div>
                    <div style="display: flex; gap: 8px;">
                      <button class="btn btn-secondary open-ws-btn" data-id="${w.id}">Open &rarr;</button>
                      <button class="btn btn-danger remove-ws-btn" data-id="${w.id}" data-name="${escapeHtml(w.name)}" data-path="${escapeHtml(w.path)}" style="padding: 6px 10px; font-size: 0.8rem;">🗑️ Remove</button>
                    </div>
                  </div>
                `).join('')}
              </div>`
        }
      </div>
    </div>
  `;

  benchmarkMeasure('dashboard-render', 'dashboard-render-start', 'dashboard-render-end');

  document.getElementById('dashNewWsBtn')?.addEventListener('click', showCreateWorkspaceModal);
  container.querySelectorAll('.open-ws-btn').forEach((btn) => {
    btn.addEventListener('click', () => openWorkspace(btn.dataset.id));
  });
  container.querySelectorAll('.remove-ws-btn').forEach((btn) => {
    btn.addEventListener('click', () => confirmRemoveWorkspace(btn.dataset.id, btn.dataset.name, btn.dataset.path));
  });
}

// --- Workspaces View ---
async function renderWorkspaces(container) {
  if (state.currentWorkspace) {
    renderWorkspaceStudio(container, state.currentWorkspace);
    return;
  }

  const wsRes = await apiFetch('/api/v1/workspaces');
  state.workspaces = (wsRes.ok && wsRes.data) ? wsRes.data : [];

  container.innerHTML = `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
      <h2>Workspaces</h2>
      <button class="btn btn-primary" id="createWsBtn">+ Add Workspace</button>
    </div>
    <div class="grid-cols-3">
      ${state.workspaces.map((w) => `
        <div class="card glass">
          <div class="card-title">
            <span>${escapeHtml(w.name)}</span>
            <span class="badge badge-provider">${w.settings?.defaultProviderId || 'antigravity'}</span>
          </div>
          <div class="card-subtitle" style="word-break: break-all;">${escapeHtml(w.path)}</div>
          <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 14px;">
            <span style="font-size: 0.8rem; color: var(--text-muted);">${w.conversationCount} conversations</span>
            <div style="display: flex; gap: 6px;">
              <button class="btn btn-danger remove-ws-btn" data-id="${w.id}" data-name="${escapeHtml(w.name)}" data-path="${escapeHtml(w.path)}" style="padding: 6px 10px; font-size: 0.8rem;">🗑️</button>
              <button class="btn btn-primary open-ws-btn" data-id="${w.id}">Open Studio</button>
            </div>
          </div>
        </div>
      `).join('')}
    </div>
  `;

  document.getElementById('createWsBtn').addEventListener('click', showCreateWorkspaceModal);
  container.querySelectorAll('.open-ws-btn').forEach((btn) => {
    btn.addEventListener('click', () => openWorkspace(btn.dataset.id));
  });
  container.querySelectorAll('.remove-ws-btn').forEach((btn) => {
    btn.addEventListener('click', () => confirmRemoveWorkspace(btn.dataset.id, btn.dataset.name, btn.dataset.path));
  });
}

function confirmRemoveWorkspace(id, name, path) {
  showModal(
    'Remove Workspace',
    `
      <p>Are you sure you want to remove the workspace <strong>"${escapeHtml(name)}"</strong>?</p>
      <div style="background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.3); border-radius: 6px; padding: 12px; margin-top: 14px; font-size: 0.88rem; color: #a5b4fc;">
        ℹ️ <strong>Note:</strong> This only removes the workspace from AI Agent Hub. Your local folder and project files at <code>${escapeHtml(path)}</code> will <strong>NOT</strong> be deleted.
      </div>
    `,
    `
      <button class="btn btn-secondary" onclick="hideModal()">Cancel</button>
      <button class="btn btn-danger" id="confirmDeleteWsBtn">Remove Workspace</button>
    `
  );

  document.getElementById('confirmDeleteWsBtn')?.addEventListener('click', async () => {
    const res = await apiFetch(`/api/v1/workspaces/${id}`, { method: 'DELETE' });
    hideModal();
    if (res.ok || res.status === 204) {
      showToast(`Workspace "${name}" removed from Agent Hub.`, 'success');
      if (state.currentWorkspace?.id === id) {
        state.currentWorkspace = null;
      }
      renderActiveTab();
    } else {
      showToast('Failed to remove workspace.', 'error');
    }
  });
}

async function openWorkspace(id) {
  navigateTo(`/workspaces/${id}`);
}

async function renderWorkspaceStudio(container, ws) {
  const treeRes = await apiFetch(`/api/v1/filesystem/tree?workspaceId=${ws.id}`);
  const treeData = (treeRes.ok && treeRes.data) ? treeRes.data : null;

  container.innerHTML = `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px;">
      <div>
        <button class="btn btn-secondary" id="backToWsList">&larr; Workspaces</button>
        <strong style="margin-left: 12px; font-size: 1.1rem;">${escapeHtml(ws.name)}</strong>
        <span style="font-size: 0.8rem; color: var(--text-muted); margin-left: 8px;">(${escapeHtml(ws.path)})</span>
      </div>
      <div style="display: flex; gap: 8px;">
        <button class="btn btn-secondary" id="viewDiffsBtn">📝 Diffs & Rollback</button>
        <button class="btn btn-danger" id="removeCurrentWsBtn" style="font-size: 0.85rem;">🗑️ Remove Workspace</button>
        <button class="btn btn-primary" id="newConvBtn">+ New Conversation</button>
      </div>
    </div>

    <div class="studio-layout">
      <!-- Left Panel: Explorer & Conversations -->
      <div class="sidebar-panel glass">
        <div class="sidebar-header">
          <strong>Files & Folders</strong>
        </div>
        <div class="tree-list" id="workspaceTree">
          ${renderTreeNodes(treeData)}
        </div>
        <div class="sidebar-header" style="border-top: 1px solid var(--border-color);">
          <strong>Conversations</strong>
        </div>
        <div style="padding: 10px; overflow-y: auto; max-height: 180px;">
          ${state.conversations.map((c) => `
            <div class="tree-item ${state.currentConversation && state.currentConversation.id === c.id ? 'active' : ''} select-conv-btn" data-id="${c.id}">
              💬 ${escapeHtml(c.title)}
            </div>
          `).join('')}
        </div>
      </div>

      <!-- Right Panel: Conversation Studio -->
      <div class="chat-container glass" id="chatPanel">
        ${renderChatArea()}
      </div>
    </div>
  `;

  document.getElementById('backToWsList').addEventListener('click', () => {
    navigateTo('/workspaces');
  });

  document.getElementById('newConvBtn').addEventListener('click', async () => {
    const title = prompt('Enter conversation topic:', 'New Feature Task');
    if (!title) return;
    const res = await apiFetch('/api/v1/conversations', {
      method: 'POST',
      body: { workspaceId: ws.id, title, providerId: ws.settings?.defaultProviderId || 'antigravity' }
    });
    if (res.ok && res.data) {
      await openWorkspace(ws.id);
    }
  });

  document.getElementById('viewDiffsBtn').addEventListener('click', showDiffViewerModal);
  document.getElementById('removeCurrentWsBtn')?.addEventListener('click', () => {
    confirmRemoveWorkspace(ws.id, ws.name, ws.path);
  });

  container.querySelectorAll('.select-conv-btn').forEach((btn) => {
    btn.addEventListener('click', () => selectConversation(btn.dataset.id));
  });

  container.querySelectorAll('.file-tree-node').forEach((node) => {
    node.addEventListener('click', () => {
      previewFile(ws.id, node.dataset.path);
    });
  });

  attachChatEventListeners();
  
  requestAnimationFrame(() => {
    const messageList = document.getElementById('messageList');
    if (messageList) messageList.scrollTop = messageList.scrollHeight;
  });
}

function renderTreeNodes(node) {
  if (!node) return '<p style="padding: 10px; color: var(--text-muted);">Empty folder</p>';
  if (!node.isDirectory) {
    return `<div class="tree-item file-tree-node" data-path="${escapeHtml(node.relativePath)}">📄 ${escapeHtml(node.name)}</div>`;
  }
  return `
    <div style="margin-bottom: 4px;">
      <div class="tree-item" style="font-weight: 600;">📁 ${escapeHtml(node.name)}</div>
      <div style="padding-left: 14px;">
        ${(node.children || []).map(renderTreeNodes).join('')}
      </div>
    </div>
  `;
}

async function getProviderModels(providerId) {
  if (!providerId) return [];
  if (state.providerModels[providerId]) return state.providerModels[providerId];
  const res = await apiFetch(`/api/v1/providers/${providerId}/models`);
  if (res.ok && Array.isArray(res.data)) {
    state.providerModels[providerId] = res.data;
    return res.data;
  }
  return [];
}

async function selectConversation(convId) {
  const res = await apiFetch(`/api/v1/conversations/${convId}`);
  if (res.ok && res.data) {
    state.currentConversation = res.data;
    if (state.currentConversation.providerId) {
      await getProviderModels(state.currentConversation.providerId);
    }
    if (state.hubConnection) {
      state.hubConnection.invoke('JoinConversation', convId).catch(() => {});
    }
    const chatPanel = document.getElementById('chatPanel');
    if (chatPanel) {
      chatPanel.innerHTML = renderChatArea();
      attachChatEventListeners();
      requestAnimationFrame(() => {
        const messageList = document.getElementById('messageList');
        if (messageList) messageList.scrollTop = messageList.scrollHeight;
      });
    }
  }
}

async function loadConversationDetails(convId) {
  const res = await apiFetch(`/api/v1/conversations/${convId}`);
  if (res.ok && res.data) {
    state.currentConversation = res.data;
    if (state.currentConversation.providerId) {
      await getProviderModels(state.currentConversation.providerId);
    }
    const chatPanel = document.getElementById('chatPanel');
    if (chatPanel) {
      const messageList = document.getElementById('messageList');
      const wasAtBottom = messageList ? (messageList.scrollHeight - messageList.scrollTop - messageList.clientHeight < 50) : true;
      
      chatPanel.innerHTML = renderChatArea();
      attachChatEventListeners();
      
      if (wasAtBottom) {
        requestAnimationFrame(() => {
          const newMessageList = document.getElementById('messageList');
          if (newMessageList) newMessageList.scrollTop = newMessageList.scrollHeight;
        });
      }
    }
  }
}

function renderChatArea() {
  if (!state.currentConversation) {
    return `
      <div style="display: flex; align-items: center; justify-content: center; height: 100%; color: var(--text-muted);">
        <p>No active conversation. Create or select one from the sidebar.</p>
      </div>
    `;
  }

  const c = state.currentConversation;
  const messages = c.messages || [];
  const models = getProviderModels(c.providerId).filter((m) => m.isDisplayed !== false);

  return `
    <div class="chat-header">
      <div class="chat-header-meta" style="display: flex; align-items: center; gap: 8px; flex-wrap: wrap;">
        <strong style="font-size: 1.1rem;">${escapeHtml(c.title)}</strong>
        <span class="badge badge-provider">${c.providerId}</span>
        <select id="convModelSelect" class="form-select" style="width: auto; padding: 4px 10px; font-size: 0.85rem; background: rgba(26, 34, 52, 0.85); border: 1px solid var(--border-color); color: var(--text-main); border-radius: 6px; cursor: pointer; color-scheme: dark;" title="Active Model">
          <option value="" ${!c.modelId ? 'selected' : ''}>Default Model</option>
          ${models.map((m) => `
            <option value="${escapeHtml(m.id)}" ${c.modelId === m.id ? 'selected' : ''}>${escapeHtml(m.displayName || m.id)}</option>
          `).join('')}
        </select>
        <select id="convEffortSelect" class="form-select" style="width: auto; padding: 4px 10px; font-size: 0.85rem; background: rgba(26, 34, 52, 0.85); border: 1px solid var(--border-color); color: var(--text-main); border-radius: 6px; cursor: pointer; color-scheme: dark;" title="Reasoning Effort / Thinking Level">
          <option value="" ${!c.effort ? 'selected' : ''}>Default Effort</option>
          <option value="low" ${c.effort === 'low' ? 'selected' : ''}>Low Effort</option>
          <option value="medium" ${c.effort === 'medium' ? 'selected' : ''}>Medium Effort</option>
          <option value="high" ${c.effort === 'high' ? 'selected' : ''}>High Effort</option>
          <option value="max" ${c.effort === 'max' ? 'selected' : ''}>Max Effort</option>
        </select>
      </div>
      <button class="btn btn-danger" id="abortBtn" style="padding: 4px 10px; font-size: 0.8rem;">⏹ Abort</button>
    </div>

    <div class="chat-messages" id="messageList">
      ${messages.map((m) => `
        <div class="message-item ${m.role === 0 ? 'message-user' : 'message-assistant'}">
          <div class="message-header">
            <span>${m.role === 0 ? '👤 You' : '⚡ AI Assistant (' + (m.metadata?.providerId || c.providerId) + ')'}</span>
            <span style="margin-left: auto;">${new Date(m.createdAtUtc).toLocaleTimeString()}</span>
          </div>
          <div class="message-body markdown-rendered">
            ${formatMessageContent(m.content)}
          </div>
        </div>
      `).join('')}
      <div id="streamingAnchor"></div>
    </div>

    <div class="chat-input-bar">
      <textarea class="form-textarea chat-textarea" id="chatInput" placeholder="Type prompt or instructions for AI assistant..."></textarea>
      <div class="input-actions">
        <span style="font-size: 0.8rem; color: var(--text-muted);">Press Enter or click Send to execute prompt</span>
        <button class="btn btn-primary" id="sendPromptBtn">Send Prompt &rarr;</button>
      </div>
    </div>
  `;
}

function attachChatEventListeners() {
  const sendBtn = document.getElementById('sendPromptBtn');
  const chatInput = document.getElementById('chatInput');
  const abortBtn = document.getElementById('abortBtn');
  const modelSelect = document.getElementById('convModelSelect');
  const effortSelect = document.getElementById('convEffortSelect');

  if (state.currentConversation?.providerId) {
    ensureProviderModels(state.currentConversation.providerId);
  }

  modelSelect?.addEventListener('change', async (e) => {
    if (!state.currentConversation) return;
    const selectedModel = e.target.value;
    const res = await apiFetch(`/api/v1/conversations/${state.currentConversation.id}/model`, {
      method: 'PUT',
      body: { modelId: selectedModel, providerId: state.currentConversation.providerId, effort: state.currentConversation.effort }
    });
    if (res.ok) {
      state.currentConversation.modelId = selectedModel;
      showToast(`Active model set to: ${selectedModel || 'Default Model'}`, 'success');
    }
  });

  effortSelect?.addEventListener('change', async (e) => {
    if (!state.currentConversation) return;
    const selectedEffort = e.target.value;
    const res = await apiFetch(`/api/v1/conversations/${state.currentConversation.id}/model`, {
      method: 'PUT',
      body: { modelId: state.currentConversation.modelId, providerId: state.currentConversation.providerId, effort: selectedEffort }
    });
    if (res.ok) {
      state.currentConversation.effort = selectedEffort;
      showToast(`Reasoning effort set to: ${selectedEffort || 'Default Effort'}`, 'success');
    }
  });

  sendBtn?.addEventListener('click', submitPrompt);
  chatInput?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      submitPrompt();
    }
  });

  abortBtn?.addEventListener('click', async () => {
    if (!state.currentConversation) return;
    await apiFetch(`/api/v1/conversations/${state.currentConversation.id}/abort`, { method: 'POST' });
    showToast('Sent abort request to provider.', 'info');
  });
}

async function submitPrompt() {
  const input = document.getElementById('chatInput');
  const prompt = input?.value.trim();
  if (!prompt || !state.currentConversation) return;

  // Check provider status before sending
  const providerId = state.currentConversation.providerId;
  const statusRes = await apiFetch(`/api/v1/providers/${providerId}/status`);
  if (statusRes.ok && statusRes.data) {
    const status = statusRes.data;
    if (status.status === 'QuotaExceeded') {
      let msg = 'Provider quota exceeded.';
      if (status.quotaResetsAt) {
        const resetTime = new Date(status.quotaResetsAt);
        msg += ` Resets at ${resetTime.toLocaleString()}.`;
      } else {
        msg += ' Please check with the provider for reset time.';
      }
      showToast(msg, 'error');
      return;
    }
    if (status.status === 'NotInstalled') {
      showToast('Provider is not installed. Please install it first.', 'error');
      return;
    }
    if (status.status === 'Unauthenticated') {
      showToast('Provider requires authentication. Please authenticate first.', 'error');
      return;
    }
  }

  input.value = '';
  const convId = state.currentConversation.id;

  // Append user message immediately
  const messageList = document.getElementById('messageList');
  if (messageList) {
    const userDiv = document.createElement('div');
    userDiv.className = 'message-item message-user';
    userDiv.innerHTML = `
      <div class="message-header"><span>👤 You</span><span style="margin-left:auto;">${new Date().toLocaleTimeString()}</span></div>
      <div class="message-body">${escapeHtml(prompt)}</div>
    `;
    messageList.appendChild(userDiv);

    // Create assistant placeholder for streaming
    const assistantDiv = document.createElement('div');
    assistantDiv.className = 'message-item message-assistant';
    assistantDiv.id = 'activeStreamingMsg';
    assistantDiv.innerHTML = `
      <div class="message-header"><span>⚡ AI Assistant (${state.currentConversation.providerId})</span><span style="margin-left:auto;">Streaming...</span></div>
      <div class="message-body markdown-rendered" id="streamingBody"><em>Thinking...</em></div>
    `;
    messageList.appendChild(assistantDiv);
    messageList.scrollTop = messageList.scrollHeight;
  }

  await apiFetch(`/api/v1/conversations/${convId}/prompt`, {
    method: 'POST',
    body: { prompt }
  });
}

function appendStreamChunk(chunk) {
  let body = document.getElementById('streamingBody');
  if (!body) {
    const messageList = document.getElementById('messageList');
    if (messageList) {
      const assistantDiv = document.createElement('div');
      assistantDiv.className = 'message-item message-assistant';
      assistantDiv.id = 'activeStreamingMsg';
      assistantDiv.innerHTML = `
        <div class="message-header"><span>⚡ AI Assistant</span><span style="margin-left:auto;">Streaming...</span></div>
        <div class="message-body markdown-rendered" id="streamingBody"></div>
      `;
      messageList.appendChild(assistantDiv);
      body = document.getElementById('streamingBody');
    }
  }

  if (body) {
    if (body.innerHTML.includes('Thinking...')) {
      body.innerHTML = '';
    }
    body.innerHTML += escapeHtml(chunk).replace(/\n/g, '<br/>');
    const messageList = document.getElementById('messageList');
    if (messageList) messageList.scrollTop = messageList.scrollHeight;
  }
}

function formatMessageContent(content) {
  if (!content) return '';
  return content
    .replace(/```([\s\S]*?)```/g, '<pre class="code-block"><code>$1</code></pre>')
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/`([^`]+)`/g, '<code class="inline-code">$1</code>')
    .replace(/\n/g, '<br/>');
}

function getProviderModels(providerId) {
  if (!providerId) return [];
  if (state.providerModels[providerId] && state.providerModels[providerId].length > 0) {
    return state.providerModels[providerId];
  }
  const provider = (state.providers || []).find((p) => p.id === providerId);
  if (provider && provider.supportedModels && provider.supportedModels.length > 0) {
    state.providerModels[providerId] = provider.supportedModels;
    return provider.supportedModels;
  }
  return [];
}

async function ensureProviderModels(providerId) {
  if (!providerId) return;
  const existing = getProviderModels(providerId);
  if (existing.length === 0) {
    const res = await apiFetch(`/api/v1/providers/${providerId}/models`);
    if (res.ok && res.data) {
      state.providerModels[providerId] = res.data;
    }
  }
  const selectEl = document.getElementById('convModelSelect');
  if (selectEl && state.currentConversation && state.currentConversation.providerId === providerId) {
    const models = getProviderModels(providerId).filter((m) => m.isDisplayed !== false);
    const currentModelId = state.currentConversation.modelId;
    selectEl.innerHTML = `
      <option value="" ${!currentModelId ? 'selected' : ''}>Default Model</option>
      ${models.map((m) => `
        <option value="${escapeHtml(m.id)}" ${currentModelId === m.id ? 'selected' : ''}>${escapeHtml(m.displayName || m.id)}</option>
      `).join('')}
    `;
  }
}

async function loadGlobalProviders() {
  const res = await apiFetch('/api/v1/providers');
  if (res.ok && res.data) {
    state.providers = res.data;
    for (const p of state.providers) {
      if (p.supportedModels && p.supportedModels.length > 0) {
        state.providerModels[p.id] = p.supportedModels;
      }
    }
    if (state.currentConversation?.providerId) {
      ensureProviderModels(state.currentConversation.providerId);
    }
  }
}

// --- Providers View ---
async function renderProviders(container) {
  benchmarkMark('providers-render-start');
  
  // Show skeletons while loading
  container.innerHTML = renderProviderSkeletons();

  // Fetch data from backend (backend handles caching)
  const res = await apiFetch('/api/v1/providers');
  state.providers = (res.ok && res.data) ? res.data : [];

  benchmarkMark('providers-render-end');

  renderProviderCards(container);

  benchmarkMeasure('providers-render', 'providers-render-start', 'providers-render-end');
}

function renderProviderCards(container) {
  container.innerHTML = `
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
      <h2>AI Providers</h2>
      <button class="btn btn-secondary" id="refreshProvBtn">🔄 Refresh All Providers</button>
    </div>
    <div class="grid-cols-3" id="providersGrid">
      ${state.providers.map((p) => {
        const statusText = p.status === 'Ready' || p.status === 2 ? 'Available' : 
                          p.status === 'NotInstalled' || p.status === 0 ? 'Not Installed' :
                          p.status === 'Unauthenticated' || p.status === 1 ? 'Not Authenticated' :
                          p.status === 'QuotaExceeded' || p.status === 5 ? 'Quota Exceeded' : 'Unknown';
        const statusClass = p.status === 'Ready' || p.status === 2 ? 'badge-provider' :
                           p.status === 'NotInstalled' || p.status === 0 ? 'badge-model' : '';
        const message = p.message || '';
        const showMessage = message ? 'block' : 'none';
        const messageBg = p.status === 'Ready' || p.status === 2 ? 'rgba(34, 197, 94, 0.1)' :
                         p.status === 'NotInstalled' || p.status === 0 ? 'rgba(239, 68, 68, 0.1)' :
                         'rgba(251, 191, 36, 0.1)';
        const messageColor = p.status === 'Ready' || p.status === 2 ? '#22c55e' :
                            p.status === 'NotInstalled' || p.status === 0 ? '#ef4444' :
                            '#fbbf24';
        return `
        <div class="card glass" id="provider-card-${p.id}">
          <div class="card-title">
            <span>${escapeHtml(p.displayName)}</span>
            <span class="badge ${statusClass}" id="provider-status-${p.id}">${statusText}</span>
          </div>
          <div class="card-subtitle">${escapeHtml(p.description)}</div>
          
          <div style="margin: 12px 0; font-size: 0.85rem;" id="provider-models-summary-${p.id}">
            <strong>Models:</strong> 
            <button class="btn-link-inline" onclick="showProviderModelsModal('${p.id}')">
              ${formatModelsSummary(p.supportedModels)}
            </button>
          </div>

          <div id="provider-message-${p.id}" style="padding: 10px; border-radius: 4px; font-size: 0.85rem; margin-bottom: 12px; display: ${showMessage}; background: ${messageBg}; color: ${messageColor};">${escapeHtml(message)}</div>

          <div id="provider-actions-${p.id}" style="display: flex; gap: 8px;">
            <button class="btn btn-secondary" onclick="refreshProvider('${p.id}')">🔄 Refresh</button>
          </div>
        </div>
      `}).join('')}
    </div>
  `;

  document.getElementById('refreshProvBtn').addEventListener('click', async () => {
    showLoadingOverlay('Refreshing all providers...');
    
    // Single batch refresh endpoint
    const res = await apiFetch('/api/v1/providers?refresh=true');
    if (res.ok && res.data) {
      state.providers = res.data;
      hideLoadingOverlay();
      showToast('All providers refreshed.', 'success');
      // Re-render cards with new data (no API call)
      renderProviderCards(document.getElementById('mainContent'));
    } else {
      hideLoadingOverlay();
      showToast('Failed to refresh providers.', 'error');
    }
  });
}

async function refreshProvider(providerId) {
  const statusBadge = document.getElementById(`provider-status-${providerId}`);
  const messageDiv = document.getElementById(`provider-message-${providerId}`);
  const actionsDiv = document.getElementById(`provider-actions-${providerId}`);
  
  if (!statusBadge || !messageDiv || !actionsDiv) return;

  statusBadge.textContent = 'Checking...';
  statusBadge.className = 'badge';
  messageDiv.style.display = 'none';

  const res = await apiFetch(`/api/v1/providers/${providerId}/status?refresh=true`);
  if (!res.ok || !res.data) {
    statusBadge.textContent = 'Error';
    statusBadge.className = 'badge badge-model';
    return;
  }

  const status = res.data;
  const rawStatus = status.status;
  const isReady = rawStatus === 'Ready' || rawStatus === 2;
  const isNotInstalled = rawStatus === 'NotInstalled' || rawStatus === 0;
  const isUnauthenticated = rawStatus === 'Unauthenticated' || rawStatus === 1;
  const isQuotaExceeded = rawStatus === 'QuotaExceeded' || rawStatus === 5;

  let statusText = 'Unknown';
  if (isReady) statusText = 'Available';
  else if (isNotInstalled) statusText = 'Not Installed';
  else if (isUnauthenticated) statusText = 'Not Authenticated';
  else if (isQuotaExceeded) statusText = 'Quota Exceeded';
  else if (rawStatus === 'Error' || rawStatus === 3) statusText = 'Error';
  else if (rawStatus === 'Running' || rawStatus === 4) statusText = 'Running';

  statusBadge.textContent = statusText;
  
  // Set badge color based on status
  if (isReady) {
    statusBadge.className = 'badge badge-provider';
    messageDiv.style.display = 'block';
    messageDiv.style.background = 'rgba(34, 197, 94, 0.1)';
    messageDiv.style.color = '#22c55e';
    messageDiv.textContent = status.message || 'Provider is ready to use.';
    actionsDiv.innerHTML = `
      <button class="btn btn-secondary" onclick="refreshProvider('${providerId}')">🔄 Refresh</button>
    `;
  } else if (isNotInstalled) {
    statusBadge.className = 'badge badge-model';
    messageDiv.style.display = 'block';
    messageDiv.style.background = 'rgba(239, 68, 68, 0.1)';
    messageDiv.style.color = '#ef4444';
    messageDiv.textContent = status.message || 'Provider is not installed.';
    actionsDiv.innerHTML = `
      <button class="btn btn-secondary" onclick="copyInstallCommand('${providerId}')"> Copy Install</button>
      <button class="btn btn-secondary" onclick="refreshProvider('${providerId}')">🔄 Refresh</button>
    `;
  } else if (isUnauthenticated) {
    statusBadge.className = 'badge' ;
    statusBadge.style.background = 'rgba(251, 191, 36, 0.2)';
    statusBadge.style.color = '#fbbf24';
    messageDiv.style.display = 'block';
    messageDiv.style.background = 'rgba(251, 191, 36, 0.1)';
    messageDiv.style.color = '#fbbf24';
    messageDiv.textContent = status.message || 'Authentication required.';
    actionsDiv.innerHTML = `
      <button class="btn btn-primary" onclick="authenticateProvider('${providerId}')">🔑 Authenticate</button>
      <button class="btn btn-secondary" onclick="refreshProvider('${providerId}')">🔄 Refresh</button>
    `;
  } else if (isQuotaExceeded) {
    statusBadge.className = 'badge' ;
    statusBadge.style.background = 'rgba(239, 68, 68, 0.2)';
    statusBadge.style.color = '#ef4444';
    messageDiv.style.display = 'block';
    messageDiv.style.background = 'rgba(239, 68, 68, 0.1)';
    messageDiv.style.color = '#ef4444';
    
    let quotaMessage = status.message || 'Quota exceeded.';
    if (status.quotaResetsAt) {
      const resetTime = new Date(status.quotaResetsAt);
      quotaMessage += ` Resets at ${resetTime.toLocaleString()}.`;
    } else {
      quotaMessage += ' Check with provider for reset time.';
    }
    messageDiv.textContent = quotaMessage;
    actionsDiv.innerHTML = `
      <button class="btn btn-secondary" onclick="refreshProvider('${providerId}')"> Refresh</button>
    `;
  } else {
    statusBadge.className = 'badge badge-model';
    messageDiv.style.display = 'block';
    messageDiv.style.background = 'rgba(156, 163, 175, 0.1)';
    messageDiv.style.color = '#9ca3af';
    messageDiv.textContent = status.message || 'Unknown status.';
    actionsDiv.innerHTML = `
      <button class="btn btn-secondary" onclick="refreshProvider('${providerId}')">🔄 Refresh</button>
    `;
  }

  // Also refresh model list and update card summary link
  const modelsRes = await apiFetch(`/api/v1/providers/${providerId}/models?refresh=true`);
  if (modelsRes.ok && modelsRes.data) {
    state.providerModels[providerId] = modelsRes.data;
    const providerObj = state.providers.find(p => p.id === providerId);
    if (providerObj) providerObj.supportedModels = modelsRes.data;
    
    const summaryContainer = document.getElementById(`provider-models-summary-${providerId}`);
    if (summaryContainer) {
      summaryContainer.innerHTML = `
        <strong>Models:</strong> 
        <button class="btn-link-inline" onclick="showProviderModelsModal('${providerId}')">
          ${formatModelsSummary(modelsRes.data)}
        </button>
      `;
    }
  }
}

function copyInstallCommand(providerId) {
  const provider = state.providers.find(p => p.id === providerId);
  if (provider && provider.installCommand) {
    navigator.clipboard.writeText(provider.installCommand);
    showToast('Copied installation command to clipboard.', 'success');
  }
}

function formatModelsSummary(models) {
  if (!models || models.length === 0) return '0 models available';
  const activeCount = models.filter(m => m.isDisplayed !== false).length;
  return `📂 ${models.length} model${models.length === 1 ? '' : 's'} available (${activeCount} active)`;
}

async function showProviderModelsModal(providerId) {
  const provider = state.providers.find((p) => p.id === providerId);
  const providerName = provider ? provider.displayName : providerId;

  let models = state.providerModels[providerId];
  if (!models) {
    const res = await apiFetch(`/api/v1/providers/${providerId}/models`);
    models = (res.ok && res.data) ? res.data : (provider?.supportedModels || []);
    state.providerModels[providerId] = models;
  }

  showModal(
    `⚙️ ${escapeHtml(providerName)} — Available Models`,
    `
      <div style="margin-bottom: 12px; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px;">
        <div style="font-size: 0.88rem; color: var(--text-muted);" id="modelCountStatus">
          Loading models...
        </div>
        <div style="display: flex; gap: 8px;">
          <button class="btn btn-secondary" id="toggleAllModelsOnBtn" style="padding: 4px 10px; font-size: 0.8rem;">Select All (ON)</button>
          <button class="btn btn-secondary" id="toggleAllModelsOffBtn" style="padding: 4px 10px; font-size: 0.8rem;">Deselect All (OFF)</button>
        </div>
      </div>

      <div class="form-group" style="margin-bottom: 12px;">
        <input type="text" id="modelSearchInput" class="form-input" placeholder="🔍 Search models by name or ID..." />
      </div>

      <div class="model-list-container" id="modelListContent">
        ${renderModelRows(models)}
      </div>
    `,
    `
      <button class="btn btn-secondary" onclick="hideModal()">Cancel</button>
      <button class="btn btn-primary" id="saveModelSettingsBtn">Save Configuration</button>
    `
  );

  const searchInput = document.getElementById('modelSearchInput');
  const countStatus = document.getElementById('modelCountStatus');
  const listContainer = document.getElementById('modelListContent');
  const toggleOnBtn = document.getElementById('toggleAllModelsOnBtn');
  const toggleOffBtn = document.getElementById('toggleAllModelsOffBtn');
  const saveBtn = document.getElementById('saveModelSettingsBtn');

  function updateStatusCount() {
    const checkboxes = listContainer.querySelectorAll('.model-toggle-checkbox');
    let total = checkboxes.length;
    let active = 0;
    checkboxes.forEach(cb => { if (cb.checked) active++; });
    countStatus.innerHTML = `<strong>${active}</strong> of <strong>${total}</strong> models configured as active (displayed).`;
  }

  updateStatusCount();

  searchInput?.addEventListener('input', (e) => {
    const query = e.target.value.toLowerCase().trim();
    listContainer.querySelectorAll('.model-row').forEach((row) => {
      const text = row.dataset.search.toLowerCase();
      row.style.display = text.includes(query) ? 'flex' : 'none';
    });
  });

  listContainer.addEventListener('change', (e) => {
    if (e.target.classList.contains('model-toggle-checkbox')) {
      updateStatusCount();
    }
  });

  toggleOnBtn?.addEventListener('click', () => {
    listContainer.querySelectorAll('.model-toggle-checkbox').forEach((cb) => {
      cb.checked = true;
    });
    updateStatusCount();
  });

  toggleOffBtn?.addEventListener('click', () => {
    listContainer.querySelectorAll('.model-toggle-checkbox').forEach((cb) => {
      cb.checked = false;
    });
    updateStatusCount();
  });

  saveBtn?.addEventListener('click', async () => {
    const modelStates = {};
    listContainer.querySelectorAll('.model-toggle-checkbox').forEach((cb) => {
      modelStates[cb.dataset.id] = cb.checked;
    });

    const res = await apiFetch(`/api/v1/providers/${providerId}/models/settings`, {
      method: 'PUT',
      body: { modelStates }
    });

    if (res.ok) {
      if (state.providerModels[providerId]) {
        state.providerModels[providerId].forEach((m) => {
          if (modelStates.hasOwnProperty(m.id)) {
            m.isDisplayed = modelStates[m.id];
          }
        });
      }
      if (provider && provider.supportedModels) {
        provider.supportedModels.forEach((m) => {
          if (modelStates.hasOwnProperty(m.id)) {
            m.isDisplayed = modelStates[m.id];
          }
        });
      }

      showToast('Model settings saved successfully.', 'success');
      hideModal();

      if (state.activeTab === 'providers') {
        renderActiveTab();
      }
    } else {
      showToast('Failed to save model settings.', 'error');
    }
  });
}

function renderModelRows(models) {
  if (!models || models.length === 0) {
    return '<div style="color: var(--text-muted); padding: 16px; text-align: center;">No models reported by this provider.</div>';
  }

  return models.map((m) => `
    <div class="model-row" data-search="${escapeHtml(m.displayName || m.id)} ${escapeHtml(m.id)}">
      <div class="model-row-info">
        <div class="model-row-title">
          ${escapeHtml(m.displayName || m.id)}
          ${m.isDefault ? '<span class="badge badge-provider" style="margin-left: 6px; font-size: 0.7rem;">Default</span>' : ''}
        </div>
        <div class="model-row-id">
          <code>${escapeHtml(m.id)}</code>
        </div>
      </div>
      <label class="toggle-switch" title="Toggle model visibility in assistant selectors">
        <input type="checkbox" class="model-toggle-checkbox" data-id="${escapeHtml(m.id)}" ${m.isDisplayed !== false ? 'checked' : ''} />
        <span class="toggle-slider"></span>
      </label>
    </div>
  `).join('');
}

async function authenticateProvider(providerId) {
  const authRes = await apiFetch(`/api/v1/providers/${providerId}/authenticate`, { method: 'POST' });
  if (authRes.ok) {
    showToast(authRes.data.message || 'Launched authentication.', 'success');
    setTimeout(() => refreshProvider(providerId), 3000);
  } else {
    showToast('Authentication failed.', 'error');
  }
}

// --- Tools (MCPs & Skills) View ---
async function renderTools(container) {
  const mcpRes = await apiFetch('/api/v1/mcps');
  const skillRes = await apiFetch('/api/v1/skills');

  const mcps = (mcpRes.ok && mcpRes.data) ? mcpRes.data : [];
  const skills = (skillRes.ok && skillRes.data) ? skillRes.data : [];

  container.innerHTML = `
    <h2>Model Context Protocol (MCP) & Provider Skills</h2>
    <p class="card-subtitle">Expose reusable tools and specialized workflows to AI coding assistants.</p>

    <div style="margin-top: 20px;" class="grid-cols-3">
      <div class="card glass">
        <div class="card-title">Registered MCP Servers</div>
        <div class="stat-val">${mcps.length}</div>
        <p class="card-subtitle" style="margin-top: 10px;">Connect external dev tools and resource providers.</p>
      </div>

      <div class="card glass">
        <div class="card-title">Installed Skills</div>
        <div class="stat-val">${skills.length}</div>
        <p class="card-subtitle" style="margin-top: 10px;">Provider-agnostic domain workflows and agents.</p>
      </div>
    </div>
  `;
}

// --- Settings View ---
async function renderSettings(container) {
  const setRes = await apiFetch('/api/v1/settings');
  const nicsRes = await apiFetch('/api/v1/settings/network-interfaces');

  const settings = (setRes.ok && setRes.data) ? setRes.data : {};
  const nics = (nicsRes.ok && nicsRes.data) ? nicsRes.data : [];

  container.innerHTML = `
    <h2>Server & Security Settings</h2>
    <p class="card-subtitle">Manage HTTPS network interface listeners, TLS certificates and administrator recovery.</p>

    <div class="card glass" style="max-width: 800px; margin-top: 20px;">
      <div class="card-title">Network Configuration</div>
      <div class="form-group">
        <label class="form-label">Network Mode</label>
        <select class="form-select" id="netModeSelect">
          <option value="0" ${settings.networkMode === 0 ? 'selected' : ''}>Localhost Only (127.0.0.1)</option>
          <option value="1" ${settings.networkMode === 1 ? 'selected' : ''}>LAN Access (All Interfaces)</option>
        </select>
      </div>

      <div class="form-group">
        <label class="form-label">Available Server Network Interfaces</label>
        <div style="background: rgba(0,0,0,0.3); padding: 12px; border-radius: 6px;">
          ${nics.map((n) => `<div>📶 <strong>${escapeHtml(n.name)}</strong> (${n.ipAddress}) - ${n.status}</div>`).join('') || '<div>127.0.0.1 (Localhost)</div>'}
        </div>
      </div>

      <div class="card-title" style="margin-top: 20px;">TLS Certificate & SANs</div>
      <p class="card-subtitle">Self-signed certificate generated with SANs covering localhost and LAN IP addresses.</p>

      <button class="btn btn-primary" id="saveSettingsBtn">Save Settings</button>
    </div>
  `;

  document.getElementById('saveSettingsBtn').addEventListener('click', async () => {
    const netMode = parseInt(document.getElementById('netModeSelect').value, 10);
    settings.networkMode = netMode;
    await apiFetch('/api/v1/settings', { method: 'PUT', body: settings });
    showToast('Settings saved successfully.', 'success');
  });
}

// --- Modals (Setup, Login, Workspace, Diffs, Preview) ---
function showModal(title, bodyHtml, footerHtml) {
  document.getElementById('modalTitle').innerText = title;
  document.getElementById('modalBody').innerHTML = bodyHtml;
  document.getElementById('modalFooter').innerHTML = footerHtml;
  document.getElementById('modalContainer').classList.remove('hidden');
}

function hideModal() {
  document.getElementById('modalContainer').classList.add('hidden');
}

function showSetupWizardModal() {
  showModal(
    'Initial Server Setup — Setup Mode',
    `
      <p class="card-subtitle">Create the single administrator account. A cryptographically secure Master Key will be initialized.</p>
      <div class="form-group">
        <label class="form-label">Admin Username</label>
        <input type="text" id="setupUsername" class="form-input" value="admin" />
      </div>
      <div class="form-group">
        <label class="form-label">Password</label>
        <input type="password" id="setupPassword" class="form-input" placeholder="At least 6 characters" />
      </div>
      <div class="form-group">
        <label class="form-label">Confirm Password</label>
        <input type="password" id="setupConfirmPassword" class="form-input" />
      </div>
    `,
    `<button class="btn btn-primary" id="setupSubmitBtn">Create Administrator Account &rarr;</button>`
  );

  document.getElementById('setupSubmitBtn').addEventListener('click', async () => {
    const username = document.getElementById('setupUsername').value;
    const password = document.getElementById('setupPassword').value;
    const confirmPassword = document.getElementById('setupConfirmPassword').value;

    const res = await apiFetch('/api/v1/auth/setup/initialize', {
      method: 'POST',
      body: { username, password, confirmPassword }
    });

    if (res.ok && res.data) {
      showModal(
        'Setup Complete — Save Recovery Code',
        `
          <p style="color: #f59e0b; margin-bottom: 12px;">⚠️ <strong>IMPORTANT:</strong> Save this recovery code securely. It is required to reset your administrator password if lost.</p>
          <div style="background: rgba(0,0,0,0.5); padding: 14px; font-family: var(--font-mono); font-size: 1.1rem; text-align: center; border-radius: 6px; letter-spacing: 2px;">
            ${res.data.recoveryCode}
          </div>
        `,
        `<button class="btn btn-primary" id="setupDoneBtn">Enter AI Agent Hub &rarr;</button>`
      );

      document.getElementById('setupDoneBtn').addEventListener('click', () => {
        state.isSetupCompleted = true;
        state.isAuthenticated = true;
        hideModal();
        renderActiveTab();
      });
    } else {
      showToast(res.data?.message || 'Setup failed.', 'error');
    }
  });
}

function renderSignInPage() {
  hideModal();
  updateUserMenuUI();

  const mainContent = document.getElementById('mainContent');
  mainContent.innerHTML = `
    <div class="auth-page-container">
      <div class="auth-card glass">
        <div class="auth-header">
          <h2>Sign In to AI Agent Hub</h2>
          <p>Enter your administrator credentials to proceed</p>
        </div>
        <div class="form-group">
          <label class="form-label">Username</label>
          <input type="text" id="loginUsername" class="form-input" value="admin" />
        </div>
        <div class="form-group">
          <label class="form-label">Password</label>
          <input type="password" id="loginPassword" class="form-input" placeholder="Enter your password" />
        </div>
        <div style="margin-top: 20px; display: flex; flex-direction: column; gap: 12px;">
          <button class="btn btn-primary" id="loginSubmitBtn" style="width: 100%;">Sign In</button>
          <div style="display: flex; justify-content: space-between; align-items: center; margin-top: 8px;">
            ${!state.isSetupCompleted ? `<button class="btn btn-secondary" id="resetSetupBtn" style="font-size: 0.8rem;">⚡ Run Setup Wizard (Create Credentials)</button>` : ''}
            ${state.isSetupCompleted ? `<a href="#" id="recoverLink" style="font-size: 0.8rem; color: #818cf8;">Lost password? Enter Recovery Code</a>` : ''}
          </div>
        </div>
      </div>
    </div>
  `;

  document.getElementById('loginSubmitBtn')?.addEventListener('click', async () => {
    const username = document.getElementById('loginUsername').value;
    const password = document.getElementById('loginPassword').value;

    const res = await apiFetch('/api/v1/auth/login', {
      method: 'POST',
      body: { username, password }
    });

    if (res.ok && res.data) {
      state.isAuthenticated = true;
      state.username = res.data.username;
      updateUserMenuUI();
      renderActiveTab();
    } else {
      showToast(res.data?.message || 'Login failed. Invalid credentials.', 'error');
    }
  });

  document.getElementById('resetSetupBtn')?.addEventListener('click', async () => {
    const res = await apiFetch('/api/v1/auth/setup/reset', { method: 'POST' });
    if (res.ok) {
      showToast('Reset to Setup Mode.', 'success');
      showSetupWizardModal();
    }
  });

  document.getElementById('recoverLink')?.addEventListener('click', (e) => {
    e.preventDefault();
    showRecoveryModal();
  });
}

function showRecoveryModal() {
  showModal(
    'Reset to Setup Mode using Recovery Code',
    `
      <p class="card-subtitle" style="margin-bottom: 12px;">Enter your 16-character recovery code (e.g. XXXX-XXXX-XXXX-XXXX) to reset the system to Setup Mode.</p>
      <div class="form-group">
        <label class="form-label">Recovery Code</label>
        <input type="text" id="recoveryCodeInput" class="form-input" placeholder="XXXX-XXXX-XXXX-XXXX" />
      </div>
      <div style="margin-top: 14px; padding: 12px; background: rgba(255,255,255,0.04); border: 1px solid var(--border-color); border-radius: 6px; font-size: 0.8rem; color: var(--text-muted); line-height: 1.4;">
        💡 <strong>Lost your recovery code?</strong> Restart the AI Agent Hub server with the <code>--recovery</code> command-line parameter. If accessed from localhost with <code>--recovery</code> enabled, an option will appear here to reset the system without a code (wiping all database data).
      </div>
      ${state.canResetWithoutCode ? `
        <div style="margin-top: 14px; padding-top: 12px; border-top: 1px solid rgba(239, 68, 68, 0.3);">
          <p style="color: #f87171; font-size: 0.82rem; margin-bottom: 8px;">⚠️ <strong>Localhost Emergency Reset Enabled (--recovery)</strong></p>
          <button class="btn btn-warning" id="resetWithoutCodeBtn" style="width: 100%; font-size: 0.85rem; background: rgba(239, 68, 68, 0.2); border: 1px solid #ef4444; color: #fca5a5;">⚡ Reset System Without Code (Erase All Data)</button>
        </div>
      ` : ''}
    `,
    `<button class="btn btn-danger" id="submitRecoveryBtn">Reset System</button>`
  );

  document.getElementById('submitRecoveryBtn')?.addEventListener('click', async () => {
    const code = document.getElementById('recoveryCodeInput').value;
    const res = await apiFetch('/api/v1/auth/recover', {
      method: 'POST',
      body: { recoveryCode: code }
    });

    if (res.ok) {
      showToast('System reset to Setup Mode.', 'success');
      state.isSetupCompleted = false;
      state.isAuthenticated = false;
      hideModal();
      showSetupWizardModal();
    } else {
      showToast(res.data?.message || 'Invalid recovery code.', 'error');
    }
  });

  document.getElementById('resetWithoutCodeBtn')?.addEventListener('click', () => {
    showFirstWipeConfirmationModal();
  });
}

function showFirstWipeConfirmationModal() {
  showModal(
    '⚠️ Warning: Forceful Data Wipe Confirmation (1/2)',
    `
      <div style="text-align: center; padding: 10px 0;">
        <span style="font-size: 3rem;">⚠️</span>
        <h4 style="color: #f87171; margin: 12px 0 6px 0; font-size: 1.1rem;">FORCE DATA WIPE WARNING</h4>
        <p style="color: var(--text-muted); font-size: 0.9rem; line-height: 1.5;">
          This action will <strong>FORCEFULLY ERASE all database data</strong> including all workspaces, user accounts, conversations, messages, file changes, encrypted secrets, and server settings.
        </p>
        <p style="color: #fca5a5; font-size: 0.85rem; margin-top: 10px; font-weight: 500;">
          This operation is permanent and CANNOT be undone.
        </p>
      </div>
    `,
    `
      <button class="btn btn-secondary" id="cancelWipe1Btn">Cancel</button>
      <button class="btn btn-danger" id="confirmWipe1Btn" style="background: #dc2626;">I Understand, Proceed &rarr;</button>
    `
  );

  document.getElementById('cancelWipe1Btn')?.addEventListener('click', () => {
    showRecoveryModal();
  });

  document.getElementById('confirmWipe1Btn')?.addEventListener('click', () => {
    showSecondWipeConfirmationModal();
  });
}

function showSecondWipeConfirmationModal() {
  showModal(
    '⛔ Final Confirmation: Erase Entire Database (2/2)',
    `
      <div style="text-align: center; padding: 10px 0;">
        <span style="font-size: 3rem;">⛔</span>
        <h4 style="color: #ef4444; margin: 12px 0 6px 0; font-size: 1.1rem;">FINAL CONFIRMATION REQUIRED</h4>
        <p style="color: var(--text-muted); font-size: 0.9rem; line-height: 1.5;">
          Are you <strong>100% SURE</strong> you want to permanently erase the entire database?
        </p>
        <p style="color: #f87171; font-size: 0.85rem; margin-top: 10px; font-weight: 600;">
          All data will be permanently purged and the system will return to Setup Mode.
        </p>
      </div>
    `,
    `
      <button class="btn btn-secondary" id="cancelWipe2Btn">Cancel</button>
      <button class="btn btn-danger" id="executeWipeBtn" style="background: #b91c1c; font-weight: 700;">⛔ CONFIRM DATA WIPE</button>
    `
  );

  document.getElementById('cancelWipe2Btn')?.addEventListener('click', () => {
    showRecoveryModal();
  });

  document.getElementById('executeWipeBtn')?.addEventListener('click', async () => {
    const res = await apiFetch('/api/v1/auth/recover-wipe', { method: 'POST' });
    if (res.ok) {
      showToast('Database wiped and system reset to Setup Mode.', 'success');
      state.isSetupCompleted = false;
      state.isAuthenticated = false;
      hideModal();
      await checkAuthAndSetup();
    } else {
      showToast(res.data?.message || 'Failed to wipe database.', 'error');
    }
  });
}

// --- Windows-Style Visual Folder Navigator Dialog ---
async function showCreateWorkspaceModal() {
  const drivesRes = await apiFetch('/api/v1/filesystem/drives');
  const drives = (drivesRes.ok && drivesRes.data) ? drivesRes.data : [];

  let currentPath = drives.length > 0 ? drives[0].path : 'D:\\';
  if (drives.some((d) => d.path.startsWith('D:') || d.name.includes('D:'))) {
    currentPath = 'D:\\';
  }

  showModal(
    'Open or Create Workspace',
    `
      <div class="form-group">
        <label class="form-label">Workspace Root Directory</label>
        <div style="display: flex; gap: 8px;">
          <input type="text" id="wsPathInput" class="form-input" value="${escapeHtml(currentPath)}" />
          <button class="btn btn-secondary" id="openNativeDialogBtn" title="Choose local folder via browser picker">📂 Local Picker...</button>
          <input type="file" id="nativeFolderPicker" webkitdirectory directory style="display: none;" />
        </div>
      </div>

      <!-- Windows-style Visual Folder Navigator -->
      <div class="explorer-dialog">
        <!-- Sidebar Quick Access & Drives -->
        <div class="explorer-sidebar" id="explorerSidebar">
          <div class="explorer-section-title">Quick Access</div>
          <div class="explorer-pin" data-path="D:\\Code">💻 Code Projects (D:\\Code)</div>
          <div class="explorer-pin" data-path="C:\\Code">💻 Code (C:\\Code)</div>
          <div class="explorer-pin" data-path="UserProfile">🏠 User Home</div>
          <div class="explorer-pin" data-path="Desktop">🖥️ Desktop</div>
          <div class="explorer-pin" data-path="Documents">📁 Documents</div>
          <div class="explorer-pin" data-path="Downloads">⬇️ Downloads</div>
          
          <div class="explorer-section-title" style="margin-top: 12px;">Drives & Partitions</div>
          ${drives.map((d) => `
            <div class="explorer-pin ${d.path.startsWith('D:') ? 'active' : ''}" data-path="${escapeHtml(d.path)}">
              💾 ${escapeHtml(d.name)} (${(d.freeSizeBytes / 1e9).toFixed(1)} GB)
            </div>
          `).join('')}
        </div>

        <!-- Main Folder Grid & Breadcrumb Bar -->
        <div class="explorer-main">
          <div class="explorer-nav-bar">
            <button class="btn btn-secondary" id="folderUpBtn" style="padding: 3px 8px; font-size: 0.8rem;">⬆️ Up</button>
            <div class="explorer-breadcrumbs" id="breadcrumbsBox">
              <!-- Breadcrumb pills rendered dynamically -->
            </div>
            <button class="btn btn-secondary" id="folderRefreshBtn" style="padding: 3px 8px; font-size: 0.8rem;">🔄</button>
          </div>

          <div class="explorer-folder-list" id="folderListBox">
            <div style="color: var(--text-muted); padding: 12px;">Loading folders...</div>
          </div>
        </div>
      </div>

      <div class="form-group" style="margin-top: 14px;">
        <label class="form-label">Workspace Display Name</label>
        <input type="text" id="wsNameInput" class="form-input" placeholder="Suggested automatically from folder name" />
      </div>

      <div class="form-group">
        <label class="form-label">Default AI Assistant Provider</label>
        <select class="form-select" id="wsProvSelect">
          <option value="antigravity" selected>Antigravity CLI (agy) — Google DeepMind</option>
          <option value="gemini">Gemini CLI</option>
          <option value="codex">OpenAI Codex CLI</option>
          <option value="claude">Claude Code</option>
          <option value="opencode">OpenCode</option>
        </select>
      </div>
    `,
    `<button class="btn btn-primary" id="submitCreateWsBtn">Create & Open Workspace &rarr;</button>`
  );

  const pathInput = document.getElementById('wsPathInput');
  const nameInput = document.getElementById('wsNameInput');
  const folderListBox = document.getElementById('folderListBox');
  const breadcrumbsBox = document.getElementById('breadcrumbsBox');
  const folderUpBtn = document.getElementById('folderUpBtn');
  const folderRefreshBtn = document.getElementById('folderRefreshBtn');
  const nativePicker = document.getElementById('nativeFolderPicker');
  const openNativeBtn = document.getElementById('openNativeDialogBtn');

  function updateSuggestedName(fullPath) {
    if (!fullPath) return;
    const clean = fullPath.replace(/[\/\\]+$/, '');
    const parts = clean.split(/[\\\/]/);
    if (parts.length > 0) {
      nameInput.value = parts[parts.length - 1] || 'Workspace';
    }
  }

  updateSuggestedName(currentPath);

  async function loadDirectory(path) {
    currentPath = path;
    pathInput.value = currentPath;
    updateSuggestedName(currentPath);

    // Update breadcrumbs
    const clean = path.replace(/[\/\\]+$/, '');
    const parts = clean.split(/[\\\/]/);
    breadcrumbsBox.innerHTML = parts.map((part, idx) => {
      const subPath = parts.slice(0, idx + 1).join('\\') + (idx === 0 ? '\\' : '');
      return `<button class="crumb-btn" data-path="${escapeHtml(subPath)}">${escapeHtml(part || '\\')}</button><span class="crumb-sep">&gt;</span>`;
    }).join('');

    breadcrumbsBox.querySelectorAll('.crumb-btn').forEach((btn) => {
      btn.addEventListener('click', () => loadDirectory(btn.dataset.path));
    });

    folderListBox.innerHTML = '<div style="color: var(--text-muted); padding: 12px;">Loading folders...</div>';

    const res = await apiFetch(`/api/v1/filesystem/browse?path=${encodeURIComponent(path)}`);
    if (res.ok && res.data) {
      const parent = res.data.parentPath;
      folderUpBtn.disabled = !parent;
      folderUpBtn.onclick = () => parent && loadDirectory(parent);

      const dirs = (res.data.entries || []).filter((e) => e.isDirectory);
      if (dirs.length === 0) {
        folderListBox.innerHTML = '<div style="color: var(--text-muted); padding: 12px; grid-column: 1 / -1;">No subdirectories in this folder.</div>';
        return;
      }

      folderListBox.innerHTML = dirs.map((d) => `
        <div class="folder-tile" data-path="${escapeHtml(d.fullPath)}" data-name="${escapeHtml(d.name)}">
          📁 <span>${escapeHtml(d.name)}</span>
        </div>
      `).join('');

      folderListBox.querySelectorAll('.folder-tile').forEach((tile) => {
        // Single click: select path
        tile.addEventListener('click', (e) => {
          folderListBox.querySelectorAll('.folder-tile').forEach((t) => t.classList.remove('selected'));
          tile.classList.add('selected');
          pathInput.value = tile.dataset.path;
          updateSuggestedName(tile.dataset.path);
        });

        // Double click: navigate into folder
        tile.addEventListener('dblclick', () => {
          loadDirectory(tile.dataset.path);
        });
      });
    } else {
      folderListBox.innerHTML = '<div style="color: var(--accent-danger); padding: 12px;">Failed to access directory.</div>';
    }
  }

  // Initial load
  await loadDirectory(currentPath);

  // Quick access sidebar clicks
  document.querySelectorAll('#explorerSidebar .explorer-pin').forEach((pin) => {
    pin.addEventListener('click', () => {
      document.querySelectorAll('#explorerSidebar .explorer-pin').forEach((p) => p.classList.remove('active'));
      pin.classList.add('active');
      const p = pin.dataset.path;
      loadDirectory(p === 'UserProfile' ? '' : p);
    });
  });

  folderRefreshBtn.addEventListener('click', () => loadDirectory(pathInput.value));
  pathInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      loadDirectory(pathInput.value);
    }
  });

  // Native folder picker
  openNativeBtn.addEventListener('click', () => nativePicker.click());
  nativePicker.addEventListener('change', (e) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      const relPath = file.webkitRelativePath || '';
      const topDir = relPath.split('/')[0];
      if (topDir) {
        nameInput.value = topDir;
        showToast(`Selected folder '${topDir}'. Verify full path in input.`, 'info');
      }
    }
  });

  document.getElementById('submitCreateWsBtn').addEventListener('click', async () => {
    const path = pathInput.value.trim();
    const name = nameInput.value.trim();
    const defaultProviderId = document.getElementById('wsProvSelect').value;

    if (!path) {
      showToast('Path is required.', 'error');
      return;
    }

    const res = await apiFetch('/api/v1/workspaces', {
      method: 'POST',
      body: { name, path, defaultProviderId }
    });

    if (res.ok && res.data) {
      hideModal();
      showToast('Workspace created successfully.', 'success');
      await openWorkspace(res.data.id);
    } else {
      showToast(res.data?.message || 'Failed to create workspace.', 'error');
    }
  });
}

async function previewFile(workspaceId, relPath) {
  const res = await apiFetch(`/api/v1/preview?workspaceId=${workspaceId}&path=${encodeURIComponent(relPath)}`);
  if (!res.ok || !res.data) {
    showToast('Failed to preview file.', 'error');
    return;
  }

  showModal(
    `Preview: ${escapeHtml(relPath)}`,
    `<div style="max-height: 60vh; overflow: auto;">${res.data.renderedHtml}</div>`,
    `<button class="btn btn-secondary" onclick="hideModal()">Close</button>`
  );
}

async function showDiffViewerModal() {
  if (!state.currentConversation || !state.currentWorkspace) return;

  const res = await apiFetch(`/api/v1/diffs?conversationId=${state.currentConversation.id}`);
  const changes = (res.ok && res.data) ? res.data : [];

  if (changes.length === 0) {
    showToast('No file modifications recorded in this conversation.', 'info');
    return;
  }

  showModal(
    'File Modifications & Diff Reviewer',
    `
      <div style="display: flex; gap: 10px; margin-bottom: 12px; overflow-x: auto;">
        ${changes.map((c, i) => `
          <button class="btn btn-secondary select-diff-btn ${i === 0 ? 'btn-primary' : ''}" data-id="${c.id}">
            ${escapeHtml(c.relativePath)} (${c.changeType === 0 ? 'Modified' : c.changeType === 1 ? 'Created' : 'Deleted'})
          </button>
        `).join('')}
      </div>
      <div id="diffViewContainer" style="max-height: 55vh; overflow: auto; background: #000; padding: 12px; border-radius: 6px;">
        Loading diff...
      </div>
    `,
    `
      <button class="btn btn-danger" id="rejectDiffBtn">❌ Reject & Rollback</button>
      <button class="btn btn-success" id="acceptDiffBtn">✔️ Accept Changes</button>
    `
  );

  let activeChangeId = changes[0].id;
  await loadDiffContent(activeChangeId);

  document.querySelectorAll('.select-diff-btn').forEach((btn) => {
    btn.addEventListener('click', async () => {
      document.querySelectorAll('.select-diff-btn').forEach((b) => b.classList.remove('btn-primary'));
      btn.classList.add('btn-primary');
      activeChangeId = btn.dataset.id;
      await loadDiffContent(activeChangeId);
    });
  });

  document.getElementById('acceptDiffBtn').addEventListener('click', async () => {
    await apiFetch(`/api/v1/diffs/${activeChangeId}/accept`, { method: 'POST' });
    showToast('Change marked as Accepted.', 'success');
    hideModal();
  });

  document.getElementById('rejectDiffBtn').addEventListener('click', async () => {
    await apiFetch(`/api/v1/diffs/${activeChangeId}/reject?workspaceId=${state.currentWorkspace.id}`, { method: 'POST' });
    showToast('File rolled back to pre-execution snapshot.', 'success');
    hideModal();
    renderActiveTab();
  });
}

async function loadDiffContent(changeId) {
  const container = document.getElementById('diffViewContainer');
  const res = await apiFetch(`/api/v1/diffs/${changeId}?workspaceId=${state.currentWorkspace.id}`);
  if (!res.ok || !res.data) {
    container.innerHTML = '<p>Diff not found.</p>';
    return;
  }

  const d = res.data;
  if (d.isBinary) {
    container.innerHTML = `
      <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px;">
        <div><strong>Original</strong><br/><img src="${d.oldContent || ''}" class="img-fluid" /></div>
        <div><strong>Modified</strong><br/><img src="${d.newContent || ''}" class="img-fluid" /></div>
      </div>
    `;
    return;
  }

  container.innerHTML = `
    <div class="diff-side-by-side">
      <div class="diff-pane" style="border-right: 1px solid var(--border-color);">
        <div style="color: var(--text-muted); margin-bottom: 8px;">ORIGINAL (BASELINE)</div>
        ${d.sideBySideLines.map((l) => `<div class="diff-line ${l.leftKind === 2 ? 'deleted' : 'unchanged'}">${l.leftLineNumber || ''} ${escapeHtml(l.leftText || '')}</div>`).join('')}
      </div>
      <div class="diff-pane">
        <div style="color: #6ee7b7; margin-bottom: 8px;">MODIFIED (CURRENT)</div>
        ${d.sideBySideLines.map((l) => `<div class="diff-line ${l.rightKind === 1 ? 'added' : 'unchanged'}">${l.rightLineNumber || ''} ${escapeHtml(l.rightText || '')}</div>`).join('')}
      </div>
    </div>
  `;
}

function showPermissionModal(req) {
  showModal(
    '⚠️ Permission Required for AI Action',
    `
      <div class="card glass" style="margin-bottom: 12px;">
        <p><strong>Provider:</strong> ${escapeHtml(req.providerId)}</p>
        <p><strong>Action Type:</strong> ${escapeHtml(req.type)}</p>
        <p><strong>Target:</strong> <code>${escapeHtml(req.target)}</code></p>
        <p><strong>Reason:</strong> ${escapeHtml(req.reason)}</p>
      </div>
      <p style="font-size: 0.85rem; color: var(--text-muted);">Please explicitly approve or deny this operation.</p>
    `,
    `
      <button class="btn btn-danger" id="denyPermBtn">Deny</button>
      <button class="btn btn-success" id="approvePermBtn">Approve & Continue</button>
    `
  );

  document.getElementById('approvePermBtn').addEventListener('click', async () => {
    await apiFetch(`/api/v1/permissions/${req.id}/decide`, { method: 'POST', body: { approve: true } });
    hideModal();
    showToast('Permission approved.', 'success');
  });

  document.getElementById('denyPermBtn').addEventListener('click', async () => {
    await apiFetch(`/api/v1/permissions/${req.id}/decide`, { method: 'POST', body: { approve: false } });
    hideModal();
    showToast('Permission denied.', 'info');
  });
}

function showToast(message, type = 'info') {
  const container = document.getElementById('toastContainer');
  if (!container) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.innerHTML = `<span>${type === 'success' ? '✔️' : type === 'error' ? '❌' : 'ℹ️'}</span> <span>${escapeHtml(message)}</span>`;
  container.appendChild(toast);

  setTimeout(() => {
    toast.remove();
  }, 4000);
}

function escapeHtml(str) {
  if (!str) return '';
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}
