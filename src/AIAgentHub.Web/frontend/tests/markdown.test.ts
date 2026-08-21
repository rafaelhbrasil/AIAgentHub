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

  it('automatically detects and collapses raw git diff output', () => {
    const rawDiff = `
diff --git a/src/MyFile.cs b/src/MyFile.cs
index c06ecbc..ce30bc0 100644
--- a/src/MyFile.cs
+++ b/src/MyFile.cs
@@ -1,15 +1,15 @@
 public class MyFile
 {
-    private int oldField = 1;
+    private int newField = 2;
     public void DoWork()
     {
+        Console.WriteLine("Added line 1");
+        Console.WriteLine("Added line 2");
+        Console.WriteLine("Added line 3");
+        Console.WriteLine("Added line 4");
+        Console.WriteLine("Added line 5");
+        Console.WriteLine("Added line 6");
     }
 }
`;
    const formatted = formatMessageContent(rawDiff);
    expect(formatted).toContain('details class="code-collapse"');
    expect(formatted).toContain('Show 19 lines of code (diff)');
    expect(formatted).toContain('language-diff');
    expect(formatted).toContain('diff --git a/src/MyFile.cs b/src/MyFile.cs');
    expect(formatted).not.toContain('<hr');
  });

  it('automatically detects and fences terminal shell sessions', () => {
    const terminal = `$ git status
On branch main
Your branch is up to date with 'origin/main'.

Changes not staged for commit:
  modified:   src/App.tsx
  modified:   src/index.css

no changes added to commit (use "git add" and/or "git commit -a")`;

    const formatted = formatMessageContent(terminal);
    expect(formatted).toContain('<pre><code class="language-bash">');
    expect(formatted).toContain('$ git status');
    expect(formatted).toContain('On branch main');
  });

  it('automatically detects and fences unfenced C# source code', () => {
    const csharpCode = `using System;
using System.Collections.Generic;

namespace MyProject.Services;

[ApiController]
public class WeatherService
{
    private readonly ILogger _logger;

    public WeatherService(ILogger logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetWeatherAsync()
    {
        return Ok(new { Temp = 22 });
    }
}`;

    const formatted = formatMessageContent(csharpCode);
    expect(formatted).toContain('details class="code-collapse"');
    expect(formatted).toContain('Show 17 lines of code (csharp)');
    expect(formatted).toContain('<pre><code class="language-csharp">');
    expect(formatted).toContain('namespace MyProject.Services;');
  });

  it('automatically detects and fences unfenced TypeScript React code', () => {
    const tsCode = `import React, { useState, useEffect } from 'react';
import { useToast } from '../context/ToastContext';

export interface UserProps {
  id: string;
  name: string;
}

export const UserCard: React.FC<UserProps> = ({ id, name }) => {
  const [active, setActive] = useState(false);
  return <div className="card">{name}</div>;
};`;

    const formatted = formatMessageContent(tsCode);
    expect(formatted).toContain('<pre><code class="language-typescript">');
    expect(formatted).toContain('import React, { useState, useEffect }');
  });

  it('preserves already fenced markdown code blocks intact', () => {
    const content = `Here is an already fenced block:

\`\`\`python
def hello_world():
    print("Hello from Python!")
\`\`\`

And standard text afterward.`;

    const formatted = formatMessageContent(content);
    expect(formatted).toContain('<pre><code class="language-python">');
    expect(formatted).toContain('def hello_world():');
    expect(formatted).toContain('Here is an already fenced block:');
  });

  it('collapses oversized single-line minified code blocks', () => {
    const minified = 'const a={};' + 'function render(){return l.jsxs("div",{className:"card glass",children:[l.jsx("span",{children:"value"})]});}'.repeat(6);
    expect(minified.length).toBeGreaterThan(400);

    const formatted = formatMessageContent(minified);
    expect(formatted).toContain('details class="code-collapse"');
    expect(formatted).toContain('Show code block');
    expect(formatted).toContain('language-javascript');
  });

  it('correctly handles real-world complex messages containing terminal commands, diffs, and conversational prose', () => {
    const complexOutput = `
> build · mimo-v2.5

$ git status
On branch refactoring
Changes not staged for commit:
  modified:   src/MyService.cs

$ git diff
diff --git a/src/MyService.cs b/src/MyService.cs
index 1234567..89abcdef 100644
--- a/src/MyService.cs
+++ b/src/MyService.cs
@@ -1,5 +1,15 @@
 public class MyService
 {
+    public void Line1() {}
+    public void Line2() {}
+    public void Line3() {}
+    public void Line4() {}
+    public void Line5() {}
+    public void Line6() {}
+    public void Line7() {}
+    public void Line8() {}
+    public void Line9() {}
+    public void Line10() {}
 }

All changes are legitimate — let me stage and commit.
`;

    const formatted = formatMessageContent(complexOutput);
    expect(formatted).toContain('language-bash');
    expect(formatted).toContain('language-diff');
    expect(formatted).toContain('details class="code-collapse"');
    expect(formatted).toContain('All changes are legitimate — let me stage and commit.');
  });

  it('colorizes unified diff lines with red and green syntax spans', () => {
    const diffBlock = `\`\`\`diff
Index: C:\\tmp\\testing\\opencode\\Example.cs
===================================================================
--- C:\\tmp\\testing\\opencode\\Example.cs
+++ C:\\tmp\\testing\\opencode\\Example.cs
@@ -17,8 +17,8 @@
         public override string ToString()
         {
-            return $"{Name} (Age: {Age})";
+            return $"{Name} (Idade: {Age})";
         }
\`\`\``;

    const formatted = formatMessageContent(diffBlock);
    expect(formatted).toContain('diff-line-header');
    expect(formatted).toContain('diff-line-hunk');
    expect(formatted).toContain('diff-line-deleted');
    expect(formatted).toContain('diff-line-added');
    expect(formatted).toContain('(Age: {Age})";</span>');
  });

  it('correctly detects and collapses unfenced tool output inline diffs with method deletions', () => {
    const rawInlineDiff = `← Edit Example.cs
Index: C:\\tmp\\testing\\opencode\\Example.cs
===================================================================
--- C:\\tmp\\testing\\opencode\\Example.cs
+++ C:\\tmp\\testing\\opencode\\Example.cs
@@ -15,13 +15,8 @@
 {
     return $"Olá, meu nome é {Name} e eu tenho {Age} anos! (edited)";
 }

-public int Multiply(int factor)
-{
-    return Age * factor;
-}

 public double Divide(int divisor)
 {
     if (divisor == 0)
         throw new DivideByZeroException("Divisor cannot be zero.");
`;

    const formatted = formatMessageContent(rawInlineDiff);
    expect(formatted).toContain('← Edit Example.cs');
    expect(formatted).toContain('details class="code-collapse"');
    expect(formatted).toContain('language-diff');
    expect(formatted).toContain('diff-line-deleted');
    expect(formatted).toContain('diff-line-header');
    expect(formatted).toContain('diff-line-hunk');
    expect(formatted).toContain('Multiply(int factor)');
  });

  it('sanitizes raw script tags and XSS injection vectors', () => {
    const malicious = '<script>alert("xss")</script>Hello World';
    const output = formatMessageContent(malicious);
    expect(output).not.toContain('<script>');
    expect(output).not.toContain('alert("xss")');
    expect(output).toContain('Hello World');
  });

  it('neutralizes onerror event handlers and javascript: pseudoprotocols', () => {
    const malicious = '<img src="x" onerror="alert(1)"><a href="javascript:alert(1)">Click Me</a>';
    const output = formatMessageContent(malicious);
    expect(output).not.toContain('onerror');
    expect(output).not.toContain('href="javascript:');
  });
});
