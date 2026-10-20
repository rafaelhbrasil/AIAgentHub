import fs from 'node:fs';
import path from 'node:path';

const rootDir = process.cwd();
const frontendDir = path.join(rootDir, 'src', 'AIAgentHub.Web', 'frontend');
const frontendLcov = path.join(frontendDir, 'coverage', 'lcov.info');
const rootCoverageDir = path.join(rootDir, 'coverage');
const rootLcov = path.join(rootCoverageDir, 'lcov.info');

function normalizeLcov(filePath) {
  if (!fs.existsSync(filePath)) return '';

  const content = fs.readFileSync(filePath, 'utf8').replaceAll('\\', '/');
  
  // Transform all SF: lines so that they are relative to repo rootDir
  const normalized = content.replace(/^SF:(.+)$/gm, (_, rawPath) => {
    let p = rawPath.trim();
    if (path.isAbsolute(p)) {
      p = path.relative(rootDir, p).replaceAll('\\', '/');
    } else if (!p.startsWith('src/AIAgentHub.Web/frontend/')) {
      const abs = path.resolve(frontendDir, p);
      p = path.relative(rootDir, abs).replaceAll('\\', '/');
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
  console.log('[normalize-lcov] Normalized and mirrored frontend LCOV with root-relative paths to both locations.');
} else if (fs.existsSync(rootLcov)) {
  normalizeLcov(rootLcov);
  console.log('[normalize-lcov] Normalized root coverage/lcov.info');
}
