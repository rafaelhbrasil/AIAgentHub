#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync, spawn, spawnSync } from 'node:child_process';

// Enforce English language across all child processes (.NET CLI, MSBuild, Git, etc.)
process.env.DOTNET_CLI_UI_LANGUAGE = 'en-US';
process.env.VSLANG = '1033';
process.env.LC_ALL = 'en_US.UTF-8';
process.env.LANG = 'en_US.UTF-8';
process.env.MSBUILDDISABLENODEREUSE = '1';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');

const args = process.argv.slice(2);
let run = false;
let foreground = false;
let port = '5001';
let protocol = 'https';
let profile = 'FolderProfile';

for (let i = 0; i < args.length; i++) {
  const arg = args[i];
  if (arg === '-r' || arg === '--run') {
    run = true;
  } else if (arg === '-f' || arg === '--foreground') {
    foreground = true;
  } else if (arg === '-p' || arg === '--port') {
    port = args[++i] || '5001';
  } else if (arg.startsWith('--port=')) {
    port = arg.split('=')[1] || '5001';
  } else if (arg === '--protocol') {
    protocol = (args[++i] || 'https').toLowerCase();
  } else if (arg.startsWith('--protocol=')) {
    protocol = (arg.split('=')[1] || 'https').toLowerCase();
  } else if (arg === '--http') {
    protocol = 'http';
  } else if (arg === '--https') {
    protocol = 'https';
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
  npm run deploy:run

Options:
  -r, --run                Run the application after publishing (detached background by default)
  -f, --foreground         Run the application attached in foreground
  -p, --port <port>        Port to bind when running (default: 5001)
  --protocol <http|https>  Default protocol (default: https)
  --http                   Shortcut for --protocol http
  --https                  Shortcut for --protocol https
  --profile <name>         Publish profile name (default: FolderProfile)
  -h, --help               Show this help message
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

// 2. Clean target directory before publishing
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

// 3. Run dotnet publish with the publish profile
console.log('📦 Executing dotnet publish...');
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
  console.error(`\n❌ Publish failed with exit code ${publishResult.status}`);
  process.exit(publishResult.status ?? 1);
}

console.log(`\n✅ Publish succeeded -> ${targetPublishDir}`);

// 4. Optionally run the published app
if (run) {
  let cmd = '';
  let cmdArgs = [];

  const winExe = path.join(targetPublishDir, 'AIAgentHub.Web.exe');
  const unixBin = path.join(targetPublishDir, 'AIAgentHub.Web');
  const dllPath = path.join(targetPublishDir, 'AIAgentHub.Web.dll');

  if (process.platform === 'win32' && fs.existsSync(winExe)) {
    cmd = winExe;
  } else if (fs.existsSync(unixBin)) {
    cmd = unixBin;
  } else if (fs.existsSync(dllPath)) {
    cmd = 'dotnet';
    cmdArgs = [dllPath];
  } else {
    console.error('❌ Could not locate runnable binary in publish folder.');
    process.exit(1);
  }

  if (protocol === 'http') {
    cmdArgs = [...cmdArgs, '--urls', `http://0.0.0.0:${port}`];
    console.log(`\n▶️  Starting application on HTTP http://0.0.0.0:${port}...`);
  } else {
    // Default HTTPS on port with HTTP fallback on port + 1
    cmdArgs = [...cmdArgs, '--port', port];
    const httpsUrl = `https://0.0.0.0:${port}`;
    const httpUrl = `http://0.0.0.0:${Number(port) + 1}`;
    console.log(`\n▶️  Starting application on ${httpsUrl} (HTTP fallback: ${httpUrl})...`);
  }

  if (foreground) {
    const child = spawn(cmd, cmdArgs, {
      stdio: 'inherit',
      cwd: targetPublishDir,
    });

    child.on('exit', (code) => {
      process.exit(code ?? 0);
    });
  } else {
    const child = spawn(cmd, cmdArgs, {
      detached: true,
      stdio: 'ignore',
      cwd: targetPublishDir,
      windowsHide: true,
    });

    child.unref();

    // Verify process is alive
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);

    let isAlive = false;
    try {
      process.kill(child.pid, 0);
      isAlive = true;
    } catch {
      isAlive = false;
    }

    if (isAlive) {
      console.log(`\n✅ AI Agent Hub is running in the background (PID: ${child.pid})`);
      if (protocol === 'http') {
        console.log(`🌐 HTTP:  http://localhost:${port}`);
      } else {
        console.log(`🔒 HTTPS: https://localhost:${port} (Default)`);
        console.log(`🌐 HTTP:  http://localhost:${Number(port) + 1}`);
      }
      process.exit(0);
    } else {
      console.error(`\n❌ Failed to start application process (PID: ${child.pid}).`);
      process.exit(1);
    }
  }
}
