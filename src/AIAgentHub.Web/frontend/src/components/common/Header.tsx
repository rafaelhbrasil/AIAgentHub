import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';

export type NavTab = 'dashboard' | 'workspaces' | 'providers' | 'tools' | 'settings';

interface HeaderProps {
  activeTab: NavTab;
  onNavigate: (tab: NavTab) => void;
}

export const Header: React.FC<HeaderProps> = ({ activeTab, onNavigate }) => {
  const { isAuthenticated, username, logout } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const handleTabClick = (tab: NavTab) => {
    onNavigate(tab);
    setMobileMenuOpen(false);
  };

  return (
    <>
      <header className="app-header">
        <div className="header-left">
          <div className="logo-badge" onClick={() => handleTabClick('dashboard')}>
            <span className="logo-icon">⚡</span>
            <div className="logo-text">
              <span className="logo-title">AI Agent Hub</span>
              <span className="logo-version">v0.1</span>
            </div>
          </div>

          {isAuthenticated && (
            <nav className="nav-links desktop-nav" id="mainNav">
              <button
                className={`nav-btn ${activeTab === 'dashboard' ? 'active' : ''}`}
                onClick={() => handleTabClick('dashboard')}
                data-tab="dashboard"
              >
                Dashboard
              </button>
              <button
                className={`nav-btn ${activeTab === 'workspaces' ? 'active' : ''}`}
                onClick={() => handleTabClick('workspaces')}
                data-tab="workspaces"
              >
                Workspaces
              </button>
              <button
                className={`nav-btn ${activeTab === 'providers' ? 'active' : ''}`}
                onClick={() => handleTabClick('providers')}
                data-tab="providers"
              >
                Providers
              </button>
              <button
                className={`nav-btn ${activeTab === 'tools' ? 'active' : ''}`}
                onClick={() => handleTabClick('tools')}
                data-tab="tools"
              >
                MCPs & Skills
              </button>
              <button
                className={`nav-btn ${activeTab === 'settings' ? 'active' : ''}`}
                onClick={() => handleTabClick('settings')}
                data-tab="settings"
              >
                Settings
              </button>
            </nav>
          )}
        </div>

        <div className="header-right">
          <div className="status-indicator online" id="serverStatus" title="Server online on HTTPS port 5432">
            <span className="status-dot"></span>
            <span className="status-label">HTTPS :5432</span>
          </div>

          {isAuthenticated && (
            <div className="user-menu" id="userMenu">
              <span className="user-avatar" id="userAvatar" title={username || 'admin'}>
                {username ? username.charAt(0).toUpperCase() : 'A'}
              </span>
              <span className="user-name" id="userNameLabel">
                {username || 'admin'}
              </span>
              <button
                className="icon-btn desktop-only"
                id="logoutBtn"
                title="Sign Out"
                aria-label="Sign Out"
                onClick={logout}
              >
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
                  <polyline points="16 17 21 12 16 7"></polyline>
                  <line x1="21" y1="12" x2="9" y2="12"></line>
                </svg>
              </button>
              <button
                type="button"
                className="icon-btn burger-menu-btn"
                aria-label="Toggle mobile menu"
                onClick={() => setMobileMenuOpen((prev) => !prev)}
              >
                {mobileMenuOpen ? '✕' : '☰'}
              </button>
            </div>
          )}
        </div>
      </header>

      {/* Mobile Drawer Dropdown */}
      {mobileMenuOpen && isAuthenticated && (
        <>
          <div className="mobile-nav-backdrop" onClick={() => setMobileMenuOpen(false)}></div>
          <nav className="mobile-nav-drawer glass">
            <button
              className={`mobile-nav-btn ${activeTab === 'dashboard' ? 'active' : ''}`}
              onClick={() => handleTabClick('dashboard')}
            >
              📊 Dashboard
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'workspaces' ? 'active' : ''}`}
              onClick={() => handleTabClick('workspaces')}
            >
              📁 Workspaces
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'providers' ? 'active' : ''}`}
              onClick={() => handleTabClick('providers')}
            >
              ⚡ Providers
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'tools' ? 'active' : ''}`}
              onClick={() => handleTabClick('tools')}
            >
              🧩 MCPs & Skills
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'settings' ? 'active' : ''}`}
              onClick={() => handleTabClick('settings')}
            >
              ⚙️ Settings
            </button>
            <div style={{ borderTop: '1px solid var(--border-color)', margin: '8px 0' }}></div>
            <button
              className="mobile-nav-btn"
              style={{ color: '#ef4444' }}
              onClick={() => {
                setMobileMenuOpen(false);
                logout();
              }}
            >
              🚪 Sign Out ({username || 'admin'})
            </button>
          </nav>
        </>
      )}
    </>
  );
};
