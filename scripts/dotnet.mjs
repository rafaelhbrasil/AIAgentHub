#!/usr/bin/env node
import { spawnSync } from 'node:child_process';

// Enforce English language across all .NET CLI & MSBuild executions
process.env.DOTNET_CLI_UI_LANGUAGE = 'en-US';
process.env.VSLANG = '1033';
process.env.LC_ALL = 'en_US.UTF-8';
process.env.LANG = 'en_US.UTF-8';
process.env.MSBUILDDISABLENODEREUSE = '1';

const args = process.argv.slice(2);
const result = spawnSync('dotnet', args, {
  stdio: 'inherit',
  env: process.env,
});

process.exit(result.status ?? 0);
