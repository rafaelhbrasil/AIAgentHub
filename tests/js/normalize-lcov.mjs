import fs from 'node:fs';
import path from 'node:path';

const frontendLcov = 'src/AIAgentHub.Web/frontend/coverage/lcov.info';
const rootCoverageDir = 'coverage';
const rootLcov = path.join(rootCoverageDir, 'lcov.info');

if (fs.existsSync(frontendLcov)) {
  const content = fs.readFileSync(frontendLcov, 'utf8').replaceAll('\\', '/');

  const absoluteContent = content.replace(/^SF:(.+)$/gm, (_, filePath) => {
    const cleanPath = filePath.trim();
    if (path.isAbsolute(cleanPath)) {
      return `SF:${cleanPath.replaceAll('\\', '/')}`;
    }
    const abs = path.resolve('src/AIAgentHub.Web/frontend', cleanPath).replaceAll('\\', '/');
    return `SF:${abs}`;
  });

  fs.writeFileSync(frontendLcov, absoluteContent);

  if (!fs.existsSync(rootCoverageDir)) {
    fs.mkdirSync(rootCoverageDir, { recursive: true });
  }

  fs.writeFileSync(rootLcov, absoluteContent);
  console.log('[normalize-lcov] Normalized and mirrored frontend LCOV with absolute paths to root coverage/lcov.info');
} else if (fs.existsSync(rootLcov)) {
  const content = fs.readFileSync(rootLcov, 'utf8').replaceAll('\\', '/');
  fs.writeFileSync(rootLcov, content);
  console.log('[normalize-lcov] Normalized root coverage/lcov.info');
}
