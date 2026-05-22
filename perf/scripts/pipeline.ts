import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dirname } from 'node:path';
import { perfConfig } from '../lib/config.js';

const rootDir = join(dirname(fileURLToPath(import.meta.url)), '..');

function run(command: string, args: string[], allowFailure = false) {
  console.log(`\n> ${command} ${args.join(' ')}`);
  const result = spawnSync(command, args, {
    cwd: rootDir,
    stdio: 'inherit',
    shell: process.platform === 'win32',
    env: process.env,
  });

  if (result.status !== 0 && !allowFailure) {
    throw new Error(`${command} failed with exit code ${result.status ?? 'unknown'}`);
  }
}

function waitForStack() {
  const waitScript = join(rootDir, '..', 'scripts', 'wait-for-stack.ps1');
  if (!existsSync(waitScript)) return;
  run('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', waitScript], true);
}

async function main() {
  console.log('TTRPG performance pipeline');
  console.log(`API=${perfConfig.apiUrl} UI=${perfConfig.uiUrl}`);

  if (process.env.PERF_SKIP_WAIT !== '1') {
    waitForStack();
  }

  if (!existsSync(join(rootDir, 'node_modules'))) {
    run('npm', ['install']);
  }

  run('npm', ['run', 'bootstrap']);

  if (process.env.PERF_SKIP_K6 !== '1') {
    run('npm', ['run', 'test:k6']);
  }

  if (process.env.PERF_SKIP_SOAK !== '1') {
    run('npm', ['run', 'soak']);
  }

  if (process.env.PERF_SKIP_UI !== '1') {
    run('npx', ['playwright', 'install', 'chromium']);
    run('npm', ['run', 'test:ui']);
  }

  run('npm', ['run', 'report']);

  console.log(`\nDone. Report: ${perfConfig.reportPath}`);
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
