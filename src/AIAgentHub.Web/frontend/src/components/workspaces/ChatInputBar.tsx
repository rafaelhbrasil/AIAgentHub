import React, { useState, useRef, useEffect } from 'react';

interface ChatInputBarProps {
  onSend: (prompt: string) => void;
  disabled?: boolean;
  isStreaming?: boolean;
  onAbort?: () => void;
}

export const ChatInputBar: React.FC<ChatInputBarProps> = ({
  onSend,
  disabled,
  isStreaming,
  onAbort,
}) => {
  const [text, setText] = useState<string>('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const isMobileDevice = () => {
    return typeof window !== 'undefined' && (window.innerWidth <= 768 || 'ontouchstart' in window);
  };

  const adjustTextareaHeight = () => {
    const textarea = textareaRef.current;
    if (!textarea) return;
    textarea.style.height = 'auto';
    // Use visualViewport on mobile (accounts for soft keyboard), fallback to window.innerHeight
    const vh = typeof window !== 'undefined'
      ? (window.visualViewport?.height ?? window.innerHeight)
      : 200;
    const maxHeight = vh * 0.3;
    const newHeight = Math.min(textarea.scrollHeight, maxHeight);
    textarea.style.height = `${Math.max(40, newHeight)}px`;
  };

  useEffect(() => {
    adjustTextareaHeight();
  }, [text]);

  // Re-adjust textarea height when mobile keyboard opens/closes
  useEffect(() => {
    const vp = window.visualViewport;
    if (!vp) return;
    const handleResize = () => adjustTextareaHeight();
    vp.addEventListener('resize', handleResize);
    return () => vp.removeEventListener('resize', handleResize);
  }, []);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    // On mobile devices, Enter inserts a line break and does NOT send
    if (isMobileDevice()) {
      return;
    }

    // On desktop, Enter sends, Shift+Enter inserts line break
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit();
    }
  };

  const handleSubmit = () => {
    const trimmed = text.trim();
    if (!trimmed || disabled) return;
    onSend(trimmed);
    setText('');
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
    }
  };

  return (
    <div className="chat-input-bar">
      <div className="chat-input-row">
        <textarea
          ref={textareaRef}
          className="form-textarea chat-textarea-auto"
          id="chatInput"
          placeholder="Type prompt or instructions for AI assistant..."
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          rows={1}
        />
        {isStreaming && onAbort && (
          <button
            type="button"
            className="round-abort-btn btn-danger abort-pulse"
            id="abortBtn"
            onClick={onAbort}
            title="Cancel ongoing response"
            aria-label="Cancel ongoing response"
          >
            <span className="abort-btn-icon">⏹</span>
          </button>
        )}
        <button
          type="button"
          className="round-send-btn btn-primary"
          id="sendPromptBtn"
          onClick={handleSubmit}
          disabled={disabled || !text.trim()}
          title="Send Prompt"
          aria-label="Send Prompt"
        >
          <span className="send-btn-icon">➤</span>
        </button>
      </div>

      <div className="input-actions desktop-hint-only">
        <span className="input-hint-text">
          Press <strong>Enter</strong> to send, <strong>Shift + Enter</strong> for line break
        </span>
      </div>
    </div>
  );
};
