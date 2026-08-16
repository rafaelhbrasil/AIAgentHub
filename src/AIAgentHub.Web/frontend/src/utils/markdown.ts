import { marked } from 'marked';

// Configure marked for full GitHub Flavored Markdown (GFM)
marked.setOptions({
  gfm: true,
  breaks: true,
});

export const ANSI_COLORS: Record<number, string> = {
  30: '#1e293b', // Black
  31: '#ef4444', // Red
  32: '#22c55e', // Green
  33: '#eab308', // Yellow
  34: '#3b82f6', // Blue
  35: '#ec4899', // Magenta
  36: '#06b6d4', // Cyan
  37: '#f8fafc', // White
  90: '#94a3b8', // Bright Black / Gray
  91: '#f87171', // Bright Red
  92: '#4ade80', // Bright Green
  93: '#fde047', // Bright Yellow
  94: '#60a5fa', // Bright Blue
  95: '#f472b6', // Bright Magenta
  96: '#22d3ee', // Bright Cyan
  97: '#ffffff', // Bright White
};

export function escapeHtml(str: string): string {
  if (!str) return '';
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

export function ansiToHtml(input: string): string {
  if (!input) return '';

  // 1. Matches ANSI SGR color/style sequences: \x1b[...m or \u001b[...m
  const ansiRegex = /(?:\x1b|\u001b)\[([0-9;]*)m/g;
  let openSpans = 0;

  let result = input.replace(ansiRegex, (_, codesStr: string) => {
    if (!codesStr || codesStr === '0') {
      const closing = '</span>'.repeat(openSpans);
      openSpans = 0;
      return closing;
    }

    const codes = codesStr.split(';').map(Number);
    const styles: string[] = [];

    for (const code of codes) {
      if (code === 0) {
        const closing = '</span>'.repeat(openSpans);
        openSpans = 0;
        return closing;
      } else if (code === 1) {
        styles.push('font-weight: 600');
      } else if (code === 2) {
        styles.push('opacity: 0.75');
      } else if (code === 3) {
        styles.push('font-style: italic');
      } else if (code === 4) {
        styles.push('text-decoration: underline');
      } else if (ANSI_COLORS[code]) {
        styles.push(`color: ${ANSI_COLORS[code]}`);
      }
    }

    if (styles.length > 0) {
      openSpans++;
      return `<span style="${styles.join('; ')}">`;
    }

    return '';
  });

  // 2. Clean any remaining non-color ANSI control sequences (cursor movement, clear line, etc.)
  result = result.replace(/(?:\x1b|\u001b)\[[0-9;?]*[A-Za-z]/g, '');

  return result + '</span>'.repeat(openSpans);
}

/**
 * Parses markdown to complete GitHub Flavored Markdown (GFM) HTML.
 */
export function renderMarkdown(content: string): string {
  if (!content) return '';
  try {
    return marked.parse(content, { async: false }) as string;
  } catch {
    return escapeHtml(content);
  }
}

/**
 * Formats AI chat message content with ANSI terminal styling and Markdown.
 */
export function formatMessageContent(content: string): string {
  if (!content) return '';

  // 1. Process ANSI terminal color & style sequences
  const textWithAnsi = ansiToHtml(content);

  // 2. Format standard Markdown with marked
  return renderMarkdown(textWithAnsi);
}
