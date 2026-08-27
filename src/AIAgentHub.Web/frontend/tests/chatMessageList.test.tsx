import { describe, it, expect } from 'vitest';
import { renderToString } from 'react-dom/server';
import { ChatMessageList } from '../src/components/workspaces/ChatMessageList';
import { MessageDto } from '../src/types/conversation';

describe('ChatMessageList', () => {
  const sampleMessages: MessageDto[] = [
    {
      id: '1',
      conversationId: 'conv-1',
      role: 'User',
      content: 'Hello AI',
      createdAtUtc: '2026-08-27T00:00:00Z',
    },
    {
      id: '2',
      conversationId: 'conv-1',
      role: 'Assistant',
      content: 'Hello! How can I help you?',
      createdAtUtc: '2026-08-27T00:00:05Z',
    },
  ];

  it('renders persisted messages without heartbeat when not streaming', () => {
    const html = renderToString(
      <ChatMessageList
        messages={sampleMessages}
        providerId="antigravity"
        isStreaming={false}
      />
    );

    expect(html).toContain('Hello AI');
    expect(html).toContain('Hello! How can I help you?');
    expect(html).not.toContain('streaming-active');
  });

  it('renders multiple heartbeat messages in sequence when streaming and no token has arrived yet', () => {
    const html = renderToString(
      <ChatMessageList
        messages={sampleMessages}
        providerId="antigravity"
        isStreaming={true}
        heartbeatMessages={[
          'Still thinking... (1m 00s elapsed)',
          'Still working on code and analysis... (2m 00s elapsed)',
          'Thinking a little longer on complex task... (3m 00s elapsed)',
        ]}
      />
    );

    expect(html).toContain('streaming-active');
    expect(html).toContain('Still thinking... (1m 00s elapsed)');
    expect(html).toContain('Still working on code and analysis... (2m 00s elapsed)');
    expect(html).toContain('Thinking a little longer on complex task... (3m 00s elapsed)');
  });

  it('renders streaming tokens and clears heartbeats once tokens arrive', () => {
    const html = renderToString(
      <ChatMessageList
        messages={sampleMessages}
        providerId="antigravity"
        isStreaming={true}
        streamingContent="Here is the actual streamed code response"
        heartbeatMessages={[]}
      />
    );

    expect(html).toContain('streaming-active');
    expect(html).toContain('Here is the actual streamed code response');
    expect(html).not.toContain('Still thinking');
  });
});
