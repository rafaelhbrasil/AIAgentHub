#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync, spawn, spawnSync } from 'node:child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');

const args = process.argv.slice(2);
let run = false;
let port = '5001';
let profile = 'FolderProfile';

for (let i = 0; i < args.length; i++) {
  const arg = args[i];
  if (arg === '-r' || arg === '--run') {
    run = true;
  } else if (arg === '-p' || arg === '--port') {
    port = args[++i] || '5001';
  } else if (arg.startsWith('--port=')) {
    port = arg.split('=')[1] || '5001';
  } else if (arg === '--profile') {
    profile = args[++i] || 'FolderProfile';
  } else if (arg.startsWith('--profile=')) {
    profile = arg.split('=')[1] || 'FolderProfile';
  } else if (arg === '-h' || arg === '--help') {
    console.log(`
AI Agent Hub - Deployment Script

Usage:
  node scripts/deploy.mjs [options]
  npm run deploy [-- [options]]

Options:
  -r, --run          Run the application after publishing
  -p, --port <port>  Port to bind when running (default: 5001)
  --profile <name>   Publish profile name (default: FolderProfile)
  -h, --help         Show this help message
`);
    process.exit(0);
  }
}

const projectDir = path.join(rootDir, 'src', 'AIAgentHub.Web');
const projectFile = path.join(projectDir, 'AIAgentHub.Web.csproj');
const pubxmlFile = path.join(projectDir, 'Properties', 'PublishProfiles', `${profile}.pubxml`);

console.log(`\n🚀 Deploying AI Agent Hub (Profile: ${profile})...`);

// 1. Resolve publish directory from pubxml or fallback
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

console.log(`📁 Target publish directory: ${targetPublishDir}`);

// 2. Check if target directory has existing content and stop locking processes
if (fs.existsSync(targetPublishDir)) {
  const contents = fs.readdirSync(targetPublishDir);
  if (contents.length > 0) {
    console.log('🔍 Checking for running instances locking the publish directory...');
    try {
      if (process.platform === 'win32') {
        execSync('taskkill /F /IM AIAgentHub.Web.exe /T', { stdio: 'ignore' });
      } else {
        execSync('pkill -f AIAgentHub.Web', { stdio: 'ignore' });
      }
      console.log('✓ Terminated running application process to release file locks.');
      // Give OS brief moment to release locks
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 300);
    } catch {
      // Process was not running, nothing to kill
    }

    try {
      fs.rmSync(targetPublishDir, { recursive: true, force: true });
    } catch { }
  }
}

// 3. Run dotnet publish with the publish profile
console.log('📦 Executing dotnet publish...');
const publishArgs = [
  'publish',
  projectFile,
  `/p:PublishProfile=${profile}`,
  `/p:PublishDir=${targetPublishDir}${path.sep}`,
];

const publishResult = spawnSync('dotnet', publishArgs, {
  stdio: 'inherit',
  cwd: rootDir,
});

if (publishResult.status !== 0) {
  console.error(`\n❌ Publish failed with exit code ${publishResult.status}`);
  process.exit(publishResult.status ?? 1);
}

console.log(`\n✅ Publish succeeded -> ${targetPublishDir}`);

// 4. Optionally run the published app
if (run) {
  const url = `http://localhost:${port}`;
  console.log(`\n▶️  Starting application on ${url}...`);

  let exePath = '';
  let cmd = '';
  let cmdArgs = [];

  const winExe = path.join(targetPublishDir, 'AIAgentHub.Web.exe');
  const unixBin = path.join(targetPublishDir, 'AIAgentHub.Web');
  const dllPath = path.join(targetPublishDir, 'AIAgentHub.Web.dll');

  if (process.platform === 'win32' && fs.existsSync(winExe)) {
    cmd = winExe;
    cmdArgs = ['--urls', url];
  } else if (fs.existsSync(unixBin)) {
    cmd = unixBin;
    cmdArgs = ['--urls', url];
  } else if (fs.existsSync(dllPath)) {
    cmd = 'dotnet';
    cmdArgs = [dllPath, '--urls', url];
  } else {
    console.error('❌ Could not locate runnable binary in publish folder.');
    process.exit(1);
  }

  const child = spawn(cmd, cmdArgs, {
    stdio: 'inherit',
    cwd: targetPublishDir,
  });

  child.on('exit', (code) => {
    process.exit(code ?? 0);
  });
}
