import fs from 'node:fs';
import path from 'node:path';

const rootDir = process.cwd();
const webProjectDir = path.join(rootDir, 'src', 'AIAgentHub.Web');
const frontendDir = path.join(webProjectDir, 'frontend');
const frontendLcov = path.join(frontendDir, 'coverage', 'lcov.info');
const rootCoverageDir = path.join(rootDir, 'coverage');
const rootLcov = path.join(rootCoverageDir, 'lcov.info');
const webCoverageDir = path.join(webProjectDir, 'coverage');
const webLcov = path.join(webCoverageDir, 'lcov.info');

function normalizeLcov(filePath) {
  if (!fs.existsSync(filePath)) return '';

  const content = fs.readFileSync(filePath, 'utf8').replaceAll('\\', '/');

  // SonarScanner for .NET evaluates LCOV for AIAgentHub.Web relative to AIAgentHub.Web project directory (src/AIAgentHub.Web).
  // Therefore, file paths must be relative to AIAgentHub.Web: "frontend/src/..."
  const normalized = content.replace(/^SF:(.+)$/gm, (_, rawPath) => {
    let p = rawPath.trim();
    if (path.isAbsolute(p)) {
      p = path.relative(webProjectDir, p).replaceAll('\\', '/');
    } else if (p.startsWith('src/AIAgentHub.Web/frontend/')) {
      p = p.replace('src/AIAgentHub.Web/', '');
    } else if (!p.startsWith('frontend/')) {
      const abs = path.resolve(frontendDir, p);
      p = path.relative(webProjectDir, abs).replaceAll('\\', '/');
    }
    return `SF:${p}`;
  });

  fs.writeFileSync(filePath, normalized);
  return normalized;
}

if (fs.existsSync(frontendLcov)) {
  const normalizedContent = normalizeLcov(frontendLcov);

  if (!fs.existsSync(rootCoverageDir)) {
    fs.mkdirSync(rootCoverageDir, { recursive: true });
  }

  fs.writeFileSync(rootLcov, normalizedContent);

  if (!fs.existsSync(webCoverageDir)) {
    fs.mkdirSync(webCoverageDir, { recursive: true });
  }

  fs.writeFileSync(webLcov, normalizedContent);
  console.log('[normalize-lcov] Normalized frontend LCOV with AIAgentHub.Web-relative paths (frontend/src/...) to all locations.');
} else if (fs.existsSync(rootLcov)) {
  normalizeLcov(rootLcov);
  console.log('[normalize-lcov] Normalized root coverage/lcov.info');
}
