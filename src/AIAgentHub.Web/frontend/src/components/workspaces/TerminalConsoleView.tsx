import React, { useEffect, useRef, useState } from 'react';
import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import '@xterm/xterm/css/xterm.css';
import { signalRService, TerminalMetaPayload } from '../../services/signalrService';

interface TerminalConsoleViewProps {
  conversationId: string;
  workspacePath: string;
  providerId?: string;
}

export const TerminalConsoleView: React.FC<TerminalConsoleViewProps> = ({
  conversationId,
  workspacePath,
  providerId,
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const terminalInstanceRef = useRef<Terminal | null>(null);
  const fitAddonRef = useRef<FitAddon | null>(null);
  const [meta, setMeta] = useState<TerminalMetaPayload>({ conversationId });

  useEffect(() => {
    if (!containerRef.current) return;

    // Create xterm.js instance with dark theme matching AgentHub aesthetic
    const term = new Terminal({
      cursorBlink: true,
      cursorStyle: 'block',
      fontFamily: 'Consolas, "Fira Code", "Cascadia Code", monospace',
      fontSize: 13,
      lineHeight: 1.25,
      scrollback: 5000,
      // ponytail: disable convertEol so TUI cursor-positioning and alternate screen buffers are not distorted
      convertEol: false,
      windowsPty: {
        backend: 'conpty',
        buildNumber: 26200,
      },
      theme: {
        background: '#0B0F19',
        foreground: '#F3F4F6',
        cursor: '#38BDF8',
        selectionBackground: 'rgba(56, 189, 248, 0.3)',
        black: '#1F2937',
        red: '#EF4444',
        green: '#10B981',
        yellow: '#F59E0B',
        blue: '#38BDF8',
        magenta: '#EC4899',
        cyan: '#06B6D4',
        white: '#F9FAFB',
        brightBlack: '#4B5563',
        brightRed: '#F87171',
        brightGreen: '#34D399',
        brightYellow: '#FBBF24',
        brightBlue: '#60A5FA',
        brightMagenta: '#F472B6',
        brightCyan: '#22D3EE',
        brightWhite: '#FFFFFF',
      },
    });

    const fitAddon = new FitAddon();
    term.loadAddon(fitAddon);
    fitAddonRef.current = fitAddon;
    terminalInstanceRef.current = term;

    term.open(containerRef.current);
    term.focus();
    (window as any).__term = term;

    // Hook up keyboard input to SignalR
    const dataDisposable = term.onData((inputData) => {
      signalRService.sendTerminalInput(conversationId, inputData).catch(() => {});
    });

    // Notify backend on terminal resize
    const resizeDisposable = term.onResize(({ cols, rows }) => {
      signalRService.resizeTerminal(conversationId, cols, rows).catch(() => {});
    });

    // Handle incoming terminal chunks
    signalRService.onTerminalOutput = (payload) => {
      if (payload.conversationId.toLowerCase() === conversationId.toLowerCase()) {
        (window as any).__chunksReceived = ((window as any).__chunksReceived || 0) + 1;
        (window as any).__lastChunk = payload.chunk;
        (window as any).__allOutput = ((window as any).__allOutput || '') + payload.chunk;
        try {
          term.write(payload.chunk);
        } catch (err) {
          console.error('[Terminal] write error:', err);
        }
      }
    };

    // Handle incoming terminal parsed metadata
    signalRService.onTerminalMeta = (payload) => {
      if (payload.conversationId.toLowerCase() === conversationId.toLowerCase()) {
        setMeta((prev) => ({ ...prev, ...payload }));
      }
    };

    // Initial fit and start session
    const animId = requestAnimationFrame(() => {
      signalRService.joinConversation(conversationId);
      try {
        fitAddon.fit();
      } catch { }
      const cols = term.cols > 0 ? term.cols : 120;
      const rows = term.rows > 0 ? term.rows : 30;
      signalRService.startTerminalSession(conversationId, workspacePath, cols, rows).catch(() => {});
      signalRService.requestTerminalHistory(conversationId).catch(() => {});
    });

    const handleResize = () => {
      try {
        fitAddon.fit();
      } catch { }
    };
    window.addEventListener('resize', handleResize);

    // ponytail: ResizeObserver guarantees terminal geometry syncs when tab or studio split view resizes
    let resizeObserver: ResizeObserver | null = null;
    if (typeof ResizeObserver !== 'undefined' && containerRef.current) {
      resizeObserver = new ResizeObserver(() => {
        try {
          fitAddon.fit();
        } catch { }
      });
      resizeObserver.observe(containerRef.current);
    }

    return () => {
      cancelAnimationFrame(animId);
      window.removeEventListener('resize', handleResize);
      resizeObserver?.disconnect();
      dataDisposable.dispose();
      resizeDisposable.dispose();
      fitAddon.dispose();
      term.dispose();
      terminalInstanceRef.current = null;
      fitAddonRef.current = null;
    };
  }, [conversationId, workspacePath]);

  const handleClear = () => {
    (window as any).__allOutput = '';
    terminalInstanceRef.current?.clear();
  };

  const handleRestart = () => {
    (window as any).__allOutput = '';
    const term = terminalInstanceRef.current;
    if (term) {
      // ponytail: reset() issues RIS (\x1bc) exiting Alternate Screen Buffer and resetting cursor coordinates
      term.reset();
      try {
        fitAddonRef.current?.fit();
      } catch { }
      term.scrollToBottom();
      term.focus();
    }
    const cols = term?.cols || 120;
    const rows = term?.rows || 30;
    signalRService.restartTerminalSession(conversationId, workspacePath, cols, rows)
      .then(() => {
        term?.focus();
        term?.scrollToBottom();
        signalRService.requestTerminalHistory(conversationId).catch(() => {});
      })
      .catch(() => {});
  };

  return (
    <div className="terminal-console-root">
      {/* Terminal Ribbon Header */}
      <div className="terminal-ribbon">
        <div className="terminal-ribbon-left">
          <span className="terminal-status-badge">
            <span className="status-dot online"></span>
            {providerId ? `${providerId} (PTY)` : 'PTY Shell'}
          </span>

          {meta.thinkingTokens != null && meta.thinkingTokens > 0 && (
            <span className="terminal-metric-badge thinking">
              🧠 Thinking: <strong>{meta.thinkingTokens.toLocaleString()}</strong> tokens
            </span>
          )}

          {meta.activeBackgroundTask && (
            <span className="terminal-metric-badge task" title={meta.activeBackgroundTask}>
              ⚡ Task: <strong>{meta.activeBackgroundTask}</strong>
            </span>
          )}

          {meta.currentTool && (
            <span className="terminal-metric-badge tool">
              🔧 Tool: <strong>{meta.currentTool}</strong>
            </span>
          )}
        </div>

        <div className="terminal-ribbon-right">
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            onClick={handleClear}
            title="Clear terminal screen"
          >
            Clear
          </button>
          <button
            type="button"
            className="btn btn-secondary compact-btn"
            onClick={handleRestart}
            title="Restart PTY console session"
          >
            Restart
          </button>
        </div>
      </div>

      {/* Embedded Terminal Container */}
      <div
        className="terminal-canvas-wrapper"
        ref={containerRef}
        onClick={() => terminalInstanceRef.current?.focus()}
      />
    </div>
  );
};
