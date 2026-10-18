import fs from 'node:fs';
import path from 'node:path';

const frontendLcov = 'src/AIAgentHub.Web/frontend/coverage/lcov.info';
const rootCoverageDir = 'coverage';
const rootLcov = path.join(rootCoverageDir, 'lcov.info');

if (fs.existsSync(frontendLcov)) {
  const content = fs.readFileSync(frontendLcov, 'utf8').replaceAll('\\', '/');
  fs.writeFileSync(frontendLcov, content);

  if (!fs.existsSync(rootCoverageDir)) {
    fs.mkdirSync(rootCoverageDir, { recursive: true });
  }

  // Prefix relative paths with the frontend subfolder path so SonarQube root scanner matches repo-relative paths
  const prefixedContent = content.replace(/^SF:(?!src\/AIAgentHub\.Web\/frontend\/)(src\/.*)$/gm, 'SF:src/AIAgentHub.Web/frontend/$1');
  fs.writeFileSync(rootLcov, prefixedContent);
  console.log('[normalize-lcov] Normalized and mirrored frontend LCOV to root coverage/lcov.info');
} else if (fs.existsSync(rootLcov)) {
  const content = fs.readFileSync(rootLcov, 'utf8').replaceAll('\\', '/');
  fs.writeFileSync(rootLcov, content);
  console.log('[normalize-lcov] Normalized root coverage/lcov.info');
}
