import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { perfConfig } from './lib/config.js';
import { login } from './lib/api.js';

export default async function globalSetup() {
  mkdirSync(join(process.cwd(), '.auth'), { recursive: true });
  const auth = await login(perfConfig.apiUrl, perfConfig.adminEmail, perfConfig.adminPassword);
  writeFileSync(
    join(process.cwd(), '.auth', 'admin-token.json'),
    JSON.stringify({ token: auth.token, email: perfConfig.adminEmail }, null, 2),
  );
}
