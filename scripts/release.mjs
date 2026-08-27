#!/usr/bin/env node
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync, spawnSync } from 'node:child_process';
import { ZipArchive } from 'archiver';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');

const args = process.argv.slice(2);
let targetVersion = null;
let skipTests = false;
let createTag = false;
let profile = 'FolderProfile';

for (let i = 0; i < args.length; i++) {
  const arg = args[i];
  if (arg === '--skip-tests') {
    skipTests = true;
  } else if (arg === '--create-tag' || arg === '-t') {
    createTag = true;
  } else if (arg === '--profile') {
    profile = args[++i] || 'FolderProfile';
  } else if (arg.startsWith('--profile=')) {
    profile = arg.split('=')[1] || 'FolderProfile';
  } else if (arg === '-h' || arg === '--help') {
    printHelp();
    process.exit(0);
  } else if (!arg.startsWith('-') && !targetVersion) {
    targetVersion = arg.trim();
  }
}

function printHelp() {
  console.log(`
AI Agent Hub - Release Packaging Script

Usage:
  npm run release [version] [options]
  node scripts/release.mjs [version] [options]

Examples:
  npm run release                # Auto-detects version from Git tag or package.json
  npm run release 0.1.0
  npm run release 0.1.0 --create-tag
  npm run release 0.2.0-beta.1

Options:
  --create-tag, -t         Create a local Git tag (e.g. v0.1.0) if it does not exist
  --skip-tests             Skip running test suites before publishing
  --profile <name>         Publish profile name (default: FolderProfile)
  -h, --help               Show this help message
`);
}

// Auto-detect version if omitted
if (!targetVersion) {
  // Try exact Git tag on HEAD first
  try {
    const gitTagResult = spawnSync('git', ['describe', '--tags', '--exact-match', 'HEAD'], {
      cwd: rootDir,
      encoding: 'utf-8',
    });
    if (gitTagResult.status === 0 && gitTagResult.stdout?.trim()) {
      targetVersion = gitTagResult.stdout.trim();
      console.log(`🏷️ Detected release version from current Git tag: ${targetVersion}`);
    }
  } catch { }

  // Fallback to root package.json version
  if (!targetVersion) {
    const rootPkg = JSON.parse(fs.readFileSync(path.join(rootDir, 'package.json'), 'utf-8'));
    targetVersion = rootPkg.version || '0.1.0';
    console.log(`📦 Using release version from package.json: ${targetVersion}`);
  }
}

// Strip leading 'v' if provided e.g. v0.1.0 -> 0.1.0
targetVersion = targetVersion.replace(/^v/i, '');

const semverRegex = /^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$/;
if (!semverRegex.test(targetVersion)) {
  console.error(`❌ Error: Invalid semantic version '${targetVersion}'. Format must be X.Y.Z (e.g. 0.1.0 or 0.2.0-beta.1).`);
  process.exit(1);
}

// 0. Validate Git tag vs HEAD state to guarantee reproducibility
try {
  const tagName = `v${targetVersion}`;
  const tagResolve = spawnSync('git', ['rev-parse', '-q', '--verify', `refs/tags/${tagName}^{commit}`], {
    cwd: rootDir,
    encoding: 'utf-8',
  });

  if (tagResolve.status === 0 && tagResolve.stdout?.trim()) {
    const tagCommit = tagResolve.stdout.trim();
    const headResolve = spawnSync('git', ['rev-parse', 'HEAD'], {
      cwd: rootDir,
      encoding: 'utf-8',
    });
    const headCommit = headResolve.stdout?.trim();

    if (headCommit && tagCommit !== headCommit) {
      console.error(`\n❌ Error: Git tag '${tagName}' already exists in this repository, but current HEAD does not point to it.`);
      console.error(`   Tagged commit:  ${tagCommit.slice(0, 8)}`);
      console.error(`   Current HEAD:   ${headCommit.slice(0, 8)}`);
      console.error(`\nTo build or verify release ${tagName}, please checkout the tag first:`);
      console.error(`  git checkout ${tagName}`);
      console.error(`  npm run release\n`);
      process.exit(1);
    }
  }
} catch { }

console.log(`\n======================================================`);
console.log(`🚀 Preparing AI Agent Hub Release: v${targetVersion}`);
console.log(`======================================================\n`);

// 1. Synchronize version across project files (only if changed)
console.log('📝 Synchronizing version across repository files...');

// 1.1 Root package.json
const rootPkgPath = path.join(rootDir, 'package.json');
const rootPkg = JSON.parse(fs.readFileSync(rootPkgPath, 'utf-8'));
if (rootPkg.version !== targetVersion) {
  rootPkg.version = targetVersion;
  fs.writeFileSync(rootPkgPath, JSON.stringify(rootPkg, null, 2) + '\n', 'utf-8');
  console.log(`  ✓ package.json -> ${targetVersion}`);
} else {
  console.log(`  ✓ package.json (already ${targetVersion})`);
}

// 1.2 Frontend package.json
const frontendPkgPath = path.join(rootDir, 'src', 'AIAgentHub.Web', 'frontend', 'package.json');
if (fs.existsSync(frontendPkgPath)) {
  const fePkg = JSON.parse(fs.readFileSync(frontendPkgPath, 'utf-8'));
  if (fePkg.version !== targetVersion) {
    fePkg.version = targetVersion;
    fs.writeFileSync(frontendPkgPath, JSON.stringify(fePkg, null, 2) + '\n', 'utf-8');
    console.log(`  ✓ src/AIAgentHub.Web/frontend/package.json -> ${targetVersion}`);
  } else {
    console.log(`  ✓ src/AIAgentHub.Web/frontend/package.json (already ${targetVersion})`);
  }
}

// 1.3 Directory.Build.props
const propsPath = path.join(rootDir, 'Directory.Build.props');
if (fs.existsSync(propsPath)) {
  let propsContent = fs.readFileSync(propsPath, 'utf-8');
  const baseVerMatch = propsContent.match(/<BaseVersion>(.*?)<\/BaseVersion>/);
  if (!baseVerMatch || baseVerMatch[1] !== targetVersion) {
    propsContent = propsContent.replace(/<BaseVersion>.*?<\/BaseVersion>/, `<BaseVersion>${targetVersion}</BaseVersion>`);
    fs.writeFileSync(propsPath, propsContent, 'utf-8');
    console.log(`  ✓ Directory.Build.props -> BaseVersion: ${targetVersion}`);
  } else {
    console.log(`  ✓ Directory.Build.props (BaseVersion already ${targetVersion})`);
  }
}

// 1.4 Changelog.md
const changelogPath = path.join(rootDir, 'docs', 'product', 'Changelog.md');
if (fs.existsSync(changelogPath)) {
  let changelogContent = fs.readFileSync(changelogPath, 'utf-8');
  const today = new Date().toISOString().split('T')[0];
  if (changelogContent.includes('## [Unreleased]')) {
    changelogContent = changelogContent.replace(
      '## [Unreleased]',
      `## [${targetVersion}] - ${today}`
    );
    fs.writeFileSync(changelogPath, changelogContent, 'utf-8');
    console.log(`  ✓ docs/product/Changelog.md -> [${targetVersion}] - ${today}`);
  }
}

// 2. Run Test Suites
if (!skipTests) {
  console.log('\n🧪 Running test suites...');
  
  console.log('  ▶️ Frontend tests (vitest)...');
  const feTest = spawnSync('npm', ['test', '-w', 'aiagenthub-frontend'], {
    stdio: 'inherit',
    cwd: rootDir,
    shell: true,
  });
  if (feTest.status !== 0) {
    console.error('❌ Frontend tests failed! Aborting release.');
    process.exit(feTest.status ?? 1);
  }

  console.log('  ▶️ Backend unit tests (dotnet test)...');
  const beTest = spawnSync('dotnet', ['test', 'tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj', '-c', 'Release'], {
    stdio: 'inherit',
    cwd: rootDir,
  });
  if (beTest.status !== 0) {
    console.error('❌ Backend unit tests failed! Aborting release.');
    process.exit(beTest.status ?? 1);
  }
  console.log('  ✅ All tests passed.');
}

// 3. Publish Release Artifacts
const projectDir = path.join(rootDir, 'src', 'AIAgentHub.Web');
const projectFile = path.join(projectDir, 'AIAgentHub.Web.csproj');
const pubxmlFile = path.join(projectDir, 'Properties', 'PublishProfiles', `${profile}.pubxml`);

let publishUrl = path.join('bin', 'Release', 'publish');
if (fs.existsSync(pubxmlFile)) {
  const content = fs.readFileSync(pubxmlFile, 'utf-8');
  const match = content.match(/<PublishUrl>(.*?)<\/PublishUrl>/i);
  if (match && match[1]?.trim()) {
    publishUrl = match[1].trim();
  }
}

const targetPublishDir = path.isAbsolute(publishUrl)
  ? publishUrl
  : path.resolve(projectDir, publishUrl);

console.log(`\n📦 Building & Publishing Release (Profile: ${profile})...`);
console.log(`📁 Target publish directory: ${targetPublishDir}`);

if (fs.existsSync(targetPublishDir)) {
  try {
    fs.rmSync(targetPublishDir, { recursive: true, force: true });
  } catch {
    console.log('🔍 Releasing locking processes for publish directory...');
    try {
      if (process.platform === 'win32') {
        execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name = 'AIAgentHub.Web.exe'\\" | Where-Object { \\$_.ExecutablePath -like '*publish*' } | ForEach-Object { Stop-Process -Id \\$_.ProcessId -Force }"`, { stdio: 'ignore' });
      } else {
        execSync('pkill -f AIAgentHub.Web', { stdio: 'ignore' });
      }
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 400);
      fs.rmSync(targetPublishDir, { recursive: true, force: true });
    } catch { }
  }
}

const publishArgs = [
  'publish',
  projectFile,
  '-c',
  'Release',
  `/p:PublishProfile=${profile}`,
  `/p:PublishDir=${targetPublishDir}${path.sep}`,
];

const publishResult = spawnSync('dotnet', publishArgs, {
  stdio: 'inherit',
  cwd: rootDir,
});

if (publishResult.status !== 0) {
  console.error(`\n❌ dotnet publish failed with exit code ${publishResult.status}`);
  process.exit(publishResult.status ?? 1);
}

console.log(`\n✅ dotnet publish succeeded -> ${targetPublishDir}`);

// 3.1 Resolve release timestamp (from SOURCE_DATE_EPOCH, Git tag/commit, or fallback)
function getReleaseTimestamp(version) {
  if (process.env.SOURCE_DATE_EPOCH) {
    const epoch = parseInt(process.env.SOURCE_DATE_EPOCH, 10);
    if (!isNaN(epoch) && epoch > 0) {
      const d = new Date(epoch * 1000);
      console.log(`⏱️ Using release timestamp from SOURCE_DATE_EPOCH: ${d.toISOString()}`);
      return d;
    }
  }

  try {
    const tagRef = `v${version}`;
    const tagCheck = spawnSync('git', ['rev-parse', '-q', '--verify', `refs/tags/${tagRef}`], {
      cwd: rootDir,
      encoding: 'utf-8',
    });

    const targetRef = (tagCheck.status === 0 && tagCheck.stdout?.trim()) ? tagRef : 'HEAD';
    const gitLog = spawnSync('git', ['log', '-1', '--format=%cI', targetRef], {
      cwd: rootDir,
      encoding: 'utf-8',
    });

    if (gitLog.status === 0 && gitLog.stdout?.trim()) {
      const gitDateStr = gitLog.stdout.trim();
      const d = new Date(gitDateStr);
      if (!isNaN(d.getTime())) {
        console.log(`⏱️ Using release timestamp from Git (${targetRef}): ${d.toISOString()}`);
        return d;
      }
    }
  } catch { }

  console.warn(`
⚠️  WARNING: Git repository / tag commit timestamp not detected and SOURCE_DATE_EPOCH is not set.
   Using fallback deterministic timestamp: 2026-01-01T00:00:00Z.
   Notice: If you are building from loose files rather than a cloned Git repository, the generated
   release checksum may not match the official GitHub release. To produce bit-for-bit identical
   checksums, please clone the Git repository with its tags or provide SOURCE_DATE_EPOCH.
`);

  return new Date('2026-01-01T00:00:00Z');
}

const releaseDate = getReleaseTimestamp(targetVersion);
const rfc1123Date = releaseDate.toUTCString();

// 3.2 Normalize ASP.NET Core staticwebassets manifest timestamps for deterministic builds
const manifestFiles = fs.readdirSync(targetPublishDir).filter(f => f.endsWith('.staticwebassets.endpoints.json'));
for (const mf of manifestFiles) {
  const mfPath = path.join(targetPublishDir, mf);
  let content = fs.readFileSync(mfPath, 'utf-8');
  content = content.replace(/\{"Name":"Last-Modified","Value":"[^"]*"\}/g, `{"Name":"Last-Modified","Value":"${rfc1123Date}"}`);
  fs.writeFileSync(mfPath, content, 'utf-8');
}

// 4. Create Archive & SHA-256 Checksum (Deterministic & Reproducible)
function getAllFilesSync(dir, baseDir = dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  let files = [];
  entries.sort((a, b) => a.name.localeCompare(b.name));
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    const relPath = path.relative(baseDir, fullPath).replace(/\\/g, '/');
    if (entry.isDirectory()) {
      files = files.concat(getAllFilesSync(fullPath, baseDir));
    } else {
      files.push({ fullPath, relPath });
    }
  }
  return files;
}

function createZipArchive(sourceDir, zipFilePath) {
  const archiveDir = path.dirname(zipFilePath);
  if (!fs.existsSync(archiveDir)) {
    fs.mkdirSync(archiveDir, { recursive: true });
  }

  return new Promise((resolve, reject) => {
    const output = fs.createWriteStream(zipFilePath);
    const archive = new ZipArchive({ zlib: { level: 9 } });

    output.on('close', () => resolve());
    archive.on('warning', (err) => {
      if (err.code === 'ENOENT') {
        console.warn('⚠️ Archiver warning:', err);
      } else {
        reject(err);
      }
    });
    archive.on('error', (err) => reject(err));
    archive.pipe(output);

    // Normalize timestamp, file order, and buffer contents for deterministic bitwise-identical builds
    const allFiles = getAllFilesSync(sourceDir);
    for (const file of allFiles) {
      const buffer = fs.readFileSync(file.fullPath);
      archive.append(buffer, { name: file.relPath, date: releaseDate, mode: 0o644 });
    }

    archive.finalize();
  });
}

function generateSha256(filePath, checksumFilePath) {
  const fileBuffer = fs.readFileSync(filePath);
  const hash = crypto.createHash('sha256').update(fileBuffer).digest('hex');
  const filename = path.basename(filePath);
  fs.writeFileSync(checksumFilePath, `${hash}  ${filename}\n`, 'utf-8');
  return hash;
}

console.log(`\n📦 Creating release archive & checksum...`);
const archiveDir = path.join(rootDir, 'archive');
const zipFilename = `AIAgentHub-v${targetVersion}.zip`;
const resolvedZipPath = path.join(archiveDir, zipFilename);
const resolvedChecksumPath = path.join(archiveDir, 'SHA256.txt');

await createZipArchive(targetPublishDir, resolvedZipPath);

const stats = fs.statSync(resolvedZipPath);
const sizeMb = (stats.size / (1024 * 1024)).toFixed(2);
const sha256Hash = generateSha256(resolvedZipPath, resolvedChecksumPath);

if (createTag) {
  const tagName = `v${targetVersion}`;
  try {
    const existingTag = spawnSync('git', ['tag', '-l', tagName], { cwd: rootDir, encoding: 'utf-8' });
    if (existingTag.stdout?.trim() === tagName) {
      console.log(`🏷️ Git tag '${tagName}' already exists.`);
    } else {
      const tagResult = spawnSync('git', ['tag', '-a', tagName, '-m', `Release ${tagName}`], { cwd: rootDir, encoding: 'utf-8' });
      if (tagResult.status === 0) {
        console.log(`🏷️ Created Git tag '${tagName}' successfully.`);
      } else {
        console.warn(`⚠️ Could not create Git tag: ${tagResult.stderr}`);
      }
    }
  } catch (err) {
    console.warn(`⚠️ Failed to create Git tag:`, err.message);
  }
}

console.log(`\n======================================================`);
console.log(`🎉 Release v${targetVersion} Created Successfully!`);
console.log(`======================================================`);
console.log(`📁 Archive:  ${path.relative(rootDir, resolvedZipPath)} (${sizeMb} MB)`);
console.log(`🔒 Checksum: ${path.relative(rootDir, resolvedChecksumPath)}`);
console.log(`🔑 SHA-256:  ${sha256Hash}`);
console.log(`======================================================\n`);
