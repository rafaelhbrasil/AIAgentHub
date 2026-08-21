import DOMPurify from 'dompurify';
import { marked } from 'marked';

// Configure marked for full GitHub Flavored Markdown (GFM)
marked.setOptions({
  gfm: true,
  breaks: true,
});

let cachedPurify: DOMPurify.DOMPurifyI | null = null;

function getPurify(): DOMPurify.DOMPurifyI | null {
  if (cachedPurify) {
    return cachedPurify;
  }

  if (typeof DOMPurify === 'function') {
    const win = typeof window !== 'undefined' ? window : (globalThis as any).window;
    if (win) {
      cachedPurify = (DOMPurify as unknown as (w: Window) => DOMPurify.DOMPurifyI)(win);
      return cachedPurify;
    }
  } else if (DOMPurify && typeof (DOMPurify as any).sanitize === 'function') {
    cachedPurify = DOMPurify as unknown as DOMPurify.DOMPurifyI;
    return cachedPurify;
  }

  return null;
}

export function sanitizeHtml(html: string): string {
  if (!html) return '';
  const purify = getPurify();
  if (purify && typeof purify.sanitize === 'function') {
    return purify.sanitize(html, {
      USE_PROFILES: { html: true },
      ADD_ATTR: ['target', 'style', 'class'],
      ADD_TAGS: ['details', 'summary'],
    });
  }
  return html;
}

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
 * Parses markdown to complete GitHub Flavored Markdown (GFM) HTML with DOMPurify sanitization.
 */
export function renderMarkdown(content: string): string {
  if (!content) return '';
  try {
    const raw = marked.parse(content, { async: false }) as string;
    return sanitizeHtml(raw);
  } catch {
    return escapeHtml(content);
  }
}

/**
 * Detects whether a line belongs to a unified diff.
 */
function isDiffLine(line: string): boolean {
  return (
    line.startsWith('diff --git ') ||
    line.startsWith('index ') ||
    line.startsWith('Index: ') ||
    line.startsWith('================') ||
    line.startsWith('--- ') ||
    line.startsWith('+++ ') ||
    line.startsWith('@@ ') ||
    line.startsWith('+') ||
    line.startsWith('-') ||
    line.startsWith(' ') ||
    line.startsWith('*** ') ||
    line.startsWith('Binary files ') ||
    line.startsWith('\\ No newline at end of file') ||
    line.startsWith('new file mode ') ||
    line.startsWith('deleted file mode ') ||
    line.startsWith('similarity index ') ||
    line.startsWith('rename from ') ||
    line.startsWith('rename to ')
  );
}

/**
 * Checks if a collection of lines is a Unified Git Diff.
 */
function isUnifiedDiffBlock(lines: string[]): boolean {
  if (lines.length < 2) return false;
  const hasDiffHeader = lines.some(
    (l) => l.startsWith('diff --git ') || l.startsWith('Index: ') || (l.startsWith('--- ') && lines.some((l2) => l2.startsWith('+++ '))) || l.startsWith('@@ ')
  );
  if (!hasDiffHeader) return false;

  const validDiffCount = lines.filter((l) => isDiffLine(l) || l.trim() === '').length;
  return validDiffCount / lines.length >= 0.8;
}

/**
 * Checks if a collection of lines is a shell or terminal command session.
 */
function isTerminalBlock(lines: string[]): boolean {
  if (lines.length < 1) return false;
  const first = lines[0].trim();
  const startsWithPrompt =
    first.startsWith('$ ') ||
    first.startsWith('> ') ||
    /^PS [A-Z]:\\/i.test(first) ||
    /^[A-Z]:\\[^>]*>/i.test(first);

  if (startsWithPrompt) return true;

  if (
    first.startsWith('On branch ') ||
    first.startsWith('Changes not staged for commit:') ||
    first.startsWith('Untracked files:') ||
    first.includes('files changed, ') ||
    /^\[[a-zA-Z0-9_\-]+ [a-f0-9]{7,}\]/.test(first)
  ) {
    return true;
  }

  return false;
}

interface CodeSignature {
  lang: string;
  patterns: RegExp[];
}

const CODE_SIGNATURES: CodeSignature[] = [
  {
    lang: 'csharp',
    patterns: [
      /^\s*using\s+[A-Za-z0-9_.]+\s*;/m,
      /^\s*namespace\s+[A-Za-z0-9_.]+/m,
      /^\s*(?:public|private|protected|internal|sealed|abstract|static|async)\s+(?:class|interface|struct|record|enum|void|Task|IActionResult|[A-Za-z0-9_<>]+)\s+[A-Za-z0-9_]+/m,
      /^\s*\[(?:HttpGet|HttpPost|HttpPut|HttpDelete|Route|Authorize|Fact|Theory|InlineData|Required|ApiController)[^\]]*\]/m,
    ],
  },
  {
    lang: 'typescript',
    patterns: [
      /^\s*import\s+(?:\{[^}]*\}|\*\s+as\s+[A-Za-z0-9_]+|[A-Za-z0-9_]+)\s+from\s+['"][^'"]+['"]/m,
      /^\s*export\s+(?:default\s+|const\s+|let\s+|function\s+|class\s+|interface\s+|type\s+|enum\s+)/m,
      /^\s*const\s+[A-Za-z0-9_$\s{},:]+\s*=\s*(?:\([^)]*\)|[A-Za-z0-9_$]+)\s*=>/m,
      /^\s*(?:interface|type)\s+[A-Za-z0-9_]+\s*(?:=\s*|\{)/m,
    ],
  },
  {
    lang: 'javascript',
    patterns: [
      /^\s*const\s+[A-Za-z0-9_$]+\s*=\s*require\(['"][^'"]+['"]\)/m,
      /^\s*module\.exports\s*=/m,
      /^\s*function\s+[A-Za-z0-9_]+\s*\(/m,
    ],
  },
  {
    lang: 'python',
    patterns: [
      /^\s*def\s+[A-Za-z0-9_]+\s*\([^)]*\)\s*:/m,
      /^\s*class\s+[A-Za-z0-9_]+(?:\([^)]*\))?\s*:/m,
      /^\s*from\s+[A-Za-z0-9_.]+\s+import\s+/m,
      /^\s*import\s+[A-Za-z0-9_.]+\s*(?:as\s+[A-Za-z0-9_]+)?$/m,
    ],
  },
  {
    lang: 'json',
    patterns: [
      /^\s*\{\s*"[A-Za-z0-9_$-]+"\s*:/m,
      /^\s*\[\s*\{\s*"[A-Za-z0-9_$-]+"\s*:/m,
    ],
  },
  {
    lang: 'html',
    patterns: [
      /^\s*<!DOCTYPE\s+html>/im,
      /^\s*<(?:html|head|body|div|template|script|style)\b[^>]*>/im,
    ],
  },
  {
    lang: 'sql',
    patterns: [
      /^\s*(?:SELECT\s+.+\s+FROM|INSERT\s+INTO|UPDATE\s+.+\s+SET|DELETE\s+FROM|CREATE\s+TABLE|ALTER\s+TABLE)\b/im,
    ],
  },
];

function detectCodeLanguage(blockText: string, lines: string[]): string | null {
  // If the block contains unified diff headers, do not misclassify as raw code
  if (
    lines.some(
      (l) =>
        l.startsWith('diff --git ') ||
        l.startsWith('Index: ') ||
        l.startsWith('--- ') ||
        l.startsWith('+++ ') ||
        l.startsWith('@@ ')
    )
  ) {
    return null;
  }

  for (const sig of CODE_SIGNATURES) {
    let matchCount = 0;
    for (const pat of sig.patterns) {
      if (pat.test(blockText)) {
        matchCount++;
      }
    }
    if (matchCount >= 1 && (lines.length >= 3 || sig.lang === 'json')) {
      return sig.lang;
    }
  }

  if (
    blockText.includes('className:') ||
    blockText.includes('createElement') ||
    blockText.includes('jsx(') ||
    blockText.includes('jsxs(') ||
    blockText.includes('.prototype.')
  ) {
    return 'javascript';
  }

  if (lines.length >= 4) {
    const codeChars = (blockText.match(/[{};()=><\[\]]/g) || []).length;
    const totalChars = blockText.replace(/\s+/g, '').length;
    if (totalChars > 30 && codeChars / totalChars > 0.12) {
      const codeLikeLines = lines.filter((l) => {
        const t = l.trim();
        return t.endsWith(';') || t.endsWith('{') || t.endsWith('}') || t.startsWith('//') || t.startsWith('/*');
      }).length;
      if (codeLikeLines / lines.length >= 0.5) {
        return 'code';
      }
    }
  }

  return null;
}

/**
 * Automatically identifies unfenced blocks of diffs, terminal sessions, and source code,
 * and encloses them with standard Markdown code fences.
 */
export function detectAndFenceUnfencedCodeBlocks(content: string): string {
  if (!content) return '';

  // Split by existing Markdown code fences to avoid modifying already-fenced code
  const parts = content.split(/(```[\s\S]*?```|~~~[\s\S]*?~~~)/g);

  return parts
    .map((part, index) => {
      // Odd indices are existing fenced code blocks -> preserve intact
      if (index % 2 === 1) {
        return part;
      }

      if (!part.trim()) {
        return part;
      }

      const lines = part.split(/\r?\n/);
      const outputLines: string[] = [];
      let i = 0;

      while (i < lines.length) {
        const line = lines[i];
        const trimmed = line.trim();

        // 1. Oversized single-line code / minified JS / JSON (> 400 chars)
        if (line.length > 400 && (line.includes('{') || line.includes('function') || line.includes('var ') || line.includes('const ') || line.includes('jsx('))) {
          outputLines.push('```javascript', line, '```');
          i++;
          continue;
        }

        // 2. Tool call output headers right above a diff (e.g. "← Edit Example.cs")
        if (
          (trimmed.startsWith('← ') || trimmed.startsWith('→ ') || trimmed.startsWith('Edit ') || trimmed.startsWith('Read ')) &&
          i + 1 < lines.length &&
          (lines[i + 1].trim().startsWith('Index: ') ||
            lines[i + 1].trim().startsWith('diff --git ') ||
            lines[i + 1].trim().startsWith('--- ') ||
            lines[i + 1].trim().startsWith('@@ '))
        ) {
          outputLines.push(line);
          i++;
          continue;
        }

        // 3. Unified Git Diff Block
        if (
          trimmed.startsWith('diff --git ') ||
          trimmed.startsWith('Index: ') ||
          (trimmed.startsWith('--- ') && i + 1 < lines.length && (lines[i + 1].trim().startsWith('+++ ') || lines[i + 1].trim().startsWith('--- '))) ||
          trimmed.startsWith('@@ ')
        ) {
          const diffLines: string[] = [];
          let inHunk = false;
          let consecutiveEmpty = 0;

          while (i < lines.length) {
            const curLine = lines[i];
            const curTrim = curLine.trim();

            if (curTrim === '') {
              consecutiveEmpty++;
              if (consecutiveEmpty >= 2) {
                break;
              }
              diffLines.push(curLine);
              i++;
              continue;
            }
            consecutiveEmpty = 0;

            if (curTrim.startsWith('@@ ')) {
              inHunk = true;
              diffLines.push(curLine);
              i++;
              continue;
            }

            if (isDiffLine(curLine)) {
              diffLines.push(curLine);
              i++;
              continue;
            }

            if (inHunk) {
              if (
                curTrim.startsWith('$ ') ||
                curTrim.startsWith('> ') ||
                /^PS [A-Z]:\\/i.test(curTrim) ||
                curTrim.startsWith('# ') ||
                curTrim.startsWith('## ') ||
                curTrim.startsWith('### ') ||
                curTrim.startsWith('← ') ||
                curTrim.startsWith('→ ')
              ) {
                break;
              }

              if (
                /^(?:I have|Here is|The method|Note that|This change|All changes|Done!|Removed|Added|Updated)\b/i.test(curTrim) &&
                !curTrim.includes(';') &&
                !curTrim.includes('{') &&
                !curTrim.includes('(')
              ) {
                break;
              }

              diffLines.push(curLine);
              i++;
              continue;
            }

            break;
          }

          if (diffLines.some((l) => l.trim().startsWith('@@ ')) || isUnifiedDiffBlock(diffLines)) {
            outputLines.push('```diff', ...diffLines, '```');
          } else {
            outputLines.push(...diffLines);
          }
          continue;
        }

        // 3. Shell / Terminal Command Session
        if (
          trimmed.startsWith('$ ') ||
          trimmed.startsWith('> ') ||
          /^PS [A-Z]:\\/i.test(trimmed) ||
          /^[A-Z]:\\[^>]*>/i.test(trimmed) ||
          trimmed.startsWith('On branch ') ||
          trimmed.startsWith('Changes not staged for commit:') ||
          trimmed.startsWith('Untracked files:')
        ) {
          const termLines: string[] = [];
          while (i < lines.length) {
            const curLine = lines[i];
            const curTrim = curLine.trim();

            // Stop on new diff or clear conversational markdown headings/quotes/bullets
            if (
              (curTrim.startsWith('diff --git ') || curTrim.startsWith('--- a/')) &&
              termLines.length > 0
            ) {
              break;
            }
            if (curTrim.startsWith('# ') || curTrim.startsWith('## ') || curTrim.startsWith('### ')) {
              break;
            }
            if (curTrim.length === 0 && i + 1 < lines.length && lines[i + 1].trim().length === 0) {
              // 2 consecutive empty lines denote paragraph break
              break;
            }

            termLines.push(curLine);
            i++;
          }

          if (isTerminalBlock(termLines)) {
            outputLines.push('```bash', ...termLines, '```');
          } else {
            outputLines.push(...termLines);
          }
          continue;
        }

        // 4. Source Code Block (3+ lines)
        if (trimmed.length > 0) {
          const candidateLines: string[] = [];
          let j = i;
          while (j < lines.length) {
            const curLine = lines[j];
            const curTrim = curLine.trim();

            if (curTrim.length === 0) {
              if (j + 1 < lines.length && lines[j + 1].trim().length === 0) {
                break;
              }
              candidateLines.push(curLine);
              j++;
              continue;
            }

            if (curTrim.startsWith('# ') || curTrim.startsWith('## ') || curTrim.startsWith('### ')) {
              break;
            }

            candidateLines.push(curLine);
            j++;
          }

          const blockText = candidateLines.join('\n');
          const detectedLang = detectCodeLanguage(blockText, candidateLines.filter((l) => l.trim().length > 0));

          if (detectedLang) {
            outputLines.push(`\`\`\`${detectedLang}`, ...candidateLines, '```');
            i = j;
            continue;
          }
        }

        outputLines.push(line);
        i++;
      }

      return outputLines.join('\n');
    })
    .join('');
}

/**
 * Colorizes unified diff lines inside <code class="language-diff"> elements with red/green syntax spans.
 */
export function colorizeDiffCodeBlocks(html: string): string {
  if (!html) return '';
  return html.replace(
    /(<code[^>]*class="[^"]*language-diff[^"]*"[^>]*>)([\s\S]*?)(<\/code>)/gi,
    (_match, openTag: string, codeContent: string, closeTag: string) => {
      const lines = codeContent.split('\n');
      if (lines.length > 0 && lines[lines.length - 1] === '') {
        lines.pop();
      }
      const formattedLines = lines.map((line) => {
        const content = line.length > 0 ? line : '&nbsp;';
        if (
          line.startsWith('+++') ||
          line.startsWith('---') ||
          line.startsWith('diff --git') ||
          line.startsWith('index ') ||
          line.startsWith('Index:') ||
          line.startsWith('================') ||
          line.startsWith('*** ')
        ) {
          return `<span class="diff-line-header">${content}</span>`;
        }
        if (line.startsWith('@@')) {
          return `<span class="diff-line-hunk">${content}</span>`;
        }
        if (line.startsWith('+')) {
          return `<span class="diff-line-added">${content}</span>`;
        }
        if (line.startsWith('-')) {
          return `<span class="diff-line-deleted">${content}</span>`;
        }
        return `<span class="diff-line-context">${content}</span>`;
      });
      return `${openTag}${formattedLines.join('')}${closeTag}`;
    }
  );
}

/**
 * Formats AI chat message content with ANSI terminal styling, automatic code block fencing, and Markdown.
 */
export function formatMessageContent(content: string): string {
  if (!content) return '';

  // 1. Process ANSI terminal color & style sequences
  const textWithAnsi = ansiToHtml(content);

  // 2. Automatically detect and fence unfenced code, diffs, and terminal outputs
  const fencedContent = detectAndFenceUnfencedCodeBlocks(textWithAnsi);

  // 3. Format standard Markdown with marked
  const rendered = renderMarkdown(fencedContent);

  // 4. Colorize diff lines inside code blocks (+ green, - red, @@ cyan)
  const colorized = colorizeDiffCodeBlocks(rendered);

  // 5. Wrap long code blocks in collapsible details elements
  const wrapped = wrapCollapsibleCodeBlocks(colorized);

  // 6. Sanitize final HTML to prevent XSS
  return sanitizeHtml(wrapped);
}

/**
 * Wraps code blocks exceeding line or character thresholds in collapsible <details> elements.
 */
export function wrapCollapsibleCodeBlocks(html: string): string {
  const COLLAPSE_LINE_THRESHOLD = 10;
  const COLLAPSE_DIFF_LINE_THRESHOLD = 5;
  const COLLAPSE_CHAR_THRESHOLD = 400;

  return html.replace(
    /<pre><code([^>]*)>([\s\S]*?)<\/code><\/pre>/gi,
    (match, codeAttr: string, codeContent: string) => {
      const rawLines = codeContent.includes('</span>')
        ? codeContent.split('</span>').filter((s) => s.trim().length > 0)
        : codeContent.split('\n');
      const meaningfulLines = rawLines.filter((l) => l.trim().length > 0);
      const lineCount = rawLines.length;
      const isDiff = /class="[^"]*language-diff/i.test(codeAttr);
      const threshold = isDiff ? COLLAPSE_DIFF_LINE_THRESHOLD : COLLAPSE_LINE_THRESHOLD;
      const isLongText = codeContent.length > COLLAPSE_CHAR_THRESHOLD;

      if (lineCount <= threshold && !isLongText) {
        return match;
      }

      // Extract language if present from class="language-xyz"
      const langMatch = codeAttr.match(/class="[^"]*language-([^"\s]+)/i);
      const langLabel = langMatch ? ` (${langMatch[1]})` : '';
      const label = meaningfulLines.length > 1
        ? `Show ${meaningfulLines.length} lines of code${langLabel}`
        : `Show code block (${Math.max(1, Math.round(codeContent.length / 1024))} KB)${langLabel}`;

      return `<details class="code-collapse"><summary>${label}</summary>${match}</details>`;
    }
  );
}
