import React, { useEffect, useRef, useState } from 'react';
import { Terminal } from '@xterm/xterm';
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

    terminalInstanceRef.current = term;
    term.open(containerRef.current);
    term.focus();

    // Hook up keyboard input to SignalR
    const dataDisposable = term.onData((inputData) => {
      signalRService.sendTerminalInput(conversationId, inputData).catch(() => {});
    });

    // Handle incoming terminal chunks
    signalRService.onTerminalOutput = (payload) => {
      if (payload.conversationId.toLowerCase() === conversationId.toLowerCase()) {
        term.write(payload.chunk);
      }
    };

    // Handle incoming terminal parsed metadata
    signalRService.onTerminalMeta = (payload) => {
      if (payload.conversationId.toLowerCase() === conversationId.toLowerCase()) {
        setMeta((prev) => ({ ...prev, ...payload }));
      }
    };

    // Start session and request history buffer
    signalRService.startTerminalSession(conversationId, workspacePath).catch(() => {});
    signalRService.requestTerminalHistory(conversationId).catch(() => {});

    const handleResize = () => {
      // In a full implementation fit addon handles geometry; basic auto-wrap handles output here
    };
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      dataDisposable.dispose();
      term.dispose();
      terminalInstanceRef.current = null;
    };
  }, [conversationId, workspacePath]);

  const handleClear = () => {
    terminalInstanceRef.current?.clear();
  };

  const handleRestart = () => {
    terminalInstanceRef.current?.clear();
    signalRService.startTerminalSession(conversationId, workspacePath).catch(() => {});
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
      <div className="terminal-canvas-wrapper" ref={containerRef} />
    </div>
  );
};
