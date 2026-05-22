import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AUTH_DIR = join(dirname(fileURLToPath(import.meta.url)), '..', '.auth');
const AUTH_CACHE_PATH = join(AUTH_DIR, 'admin-token.json');

export interface AdminAuthCache {
  token: string;
  email: string;
}

export function readAdminAuthCache(): AdminAuthCache | null {
  if (!existsSync(AUTH_CACHE_PATH)) {
    return null;
  }

  return JSON.parse(readFileSync(AUTH_CACHE_PATH, 'utf8')) as AdminAuthCache;
}

export function writeAdminAuthCache(cache: AdminAuthCache) {
  mkdirSync(AUTH_DIR, { recursive: true });
  writeFileSync(AUTH_CACHE_PATH, JSON.stringify(cache, null, 2));
}
