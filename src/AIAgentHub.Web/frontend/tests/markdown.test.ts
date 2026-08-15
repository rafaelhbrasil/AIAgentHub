import { describe, it, expect } from 'vitest';
import { escapeHtml, formatMessageContent } from '../src/utils/markdown';

describe('markdown utils', () => {
  it('escapes html entities safely', () => {
    const raw = '<script>alert("test & \'x\' > 1")</script>';
    const escaped = escapeHtml(raw);
    expect(escaped).toContain('&lt;script&gt;');
    expect(escaped).toContain('&quot;test &amp; &#039;x&#039; &gt; 1&quot;');
    expect(escaped).not.toContain('<script>');
  });

  it('formats code blocks, bold text, inline code and linebreaks', () => {
    const markdown = 'Here is **bold text** and `inline_code()`.\n```ts\nconst a = 1;\n```';
    const formatted = formatMessageContent(markdown);

    expect(formatted).toContain('<strong>bold text</strong>');
    expect(formatted).toContain('<code class="inline-code">inline_code()</code>');
    expect(formatted).toContain('<pre class="code-block"><code>');
    expect(formatted).toContain('const a = 1;');
  });

  it('converts ANSI terminal color codes to styled HTML spans', () => {
    const terminalOutput = '\x1b[91m\x1b[1mError: \x1b[0mThe model requires opt-in: https://opencode.ai/workspace';
    const formatted = formatMessageContent(terminalOutput);

    expect(formatted).toContain('style="color: #f87171"');
    expect(formatted).toContain('style="font-weight: 600"');
    expect(formatted).toContain('Error:');
    expect(formatted).toContain('The model requires opt-in:');
    expect(formatted).toContain('<a href="https://opencode.ai/workspace"');
    expect(formatted).not.toContain('[91m');
    expect(formatted).not.toContain('[0m');
  });

  it('handles empty or null content safely', () => {
    expect(escapeHtml('')).toBe('');
    expect(formatMessageContent('')).toBe('');
  });
});

