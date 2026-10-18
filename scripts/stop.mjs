#!/usr/bin/env node
import { execSync } from 'node:child_process';

// Stops the backend (AIAgentHub.Web, including `dotnet run` / deployed instances)
// and the frontend Vite dev server (port 5173, see frontend/vite.config.ts).
const win = process.platform === 'win32';
const run = (cmd) => {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch {
    return false; // nothing to kill
  }
};

const backend = win
  ? run('taskkill /F /IM AIAgentHub.Web.exe /T')
  : run('pkill -f AIAgentHub.Web');

// ponytail: frontend is identified by its dev port only; a different process on 5173 would also be killed.
const frontend = win
  ? run(
      `powershell -NoProfile -Command "$p = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction Stop | Select-Object -ExpandProperty OwningProcess -Unique; $p | ForEach-Object { Stop-Process -Id $_ -Force }"`
    )
  : run('lsof -ti tcp:5173 -sTCP:LISTEN | xargs kill -9');

console.log(`Backend (AIAgentHub.Web): ${backend ? 'stopped' : 'not running'}`);
console.log(`Frontend (Vite :5173):    ${frontend ? 'stopped' : 'not running'}`);
