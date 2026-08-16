import { describe, it, expect } from 'vitest';
import { escapeHtml, formatMessageContent, renderMarkdown } from '../src/utils/markdown';

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
    expect(formatted).toContain('<code>inline_code()</code>');
    expect(formatted).toContain('<pre><code class="language-ts">');
    expect(formatted).toContain('const a = 1;');
  });

  it('renders GFM lists, nested bullets, tables and blockquotes', () => {
    const md = `
# Title
- Feature 1
  - Sub feature A
  - Sub feature B
- Feature 2

| Key | Value |
| --- | --- |
| Provider | OpenCode |

> Important note
`;
    const html = renderMarkdown(md);

    expect(html).toContain('<h1>Title</h1>');
    expect(html).toContain('<ul>');
    expect(html).toContain('<li>Feature 1');
    expect(html).toContain('<li>Sub feature A</li>');
    expect(html).toContain('<table>');
    expect(html).toContain('<th>Key</th>');
    expect(html).toContain('<td>OpenCode</td>');
    expect(html).toContain('<blockquote>');
    expect(html).toContain('Important note');
  });

  it('converts ANSI terminal color codes to styled HTML spans', () => {
    const terminalOutput = '\x1b[91m\x1b[1mError: \x1b[0mThe model requires opt-in: [Link](https://opencode.ai/workspace)';
    const formatted = formatMessageContent(terminalOutput);

    expect(formatted).toContain('style="color: #f87171"');
    expect(formatted).toContain('style="font-weight: 600"');
    expect(formatted).toContain('Error:');
    expect(formatted).toContain('The model requires opt-in:');
    expect(formatted).toContain('<a href="https://opencode.ai/workspace">Link</a>');
    expect(formatted).not.toContain('[91m');
    expect(formatted).not.toContain('[0m');
  });

  it('handles empty or null content safely', () => {
    expect(escapeHtml('')).toBe('');
    expect(formatMessageContent('')).toBe('');
    expect(renderMarkdown('')).toBe('');
  });
});
