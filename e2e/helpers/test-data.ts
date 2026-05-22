export const E2E_ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@example.local';
export const E2E_ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? 'Password1';

export function uniqueLabel(prefix: string) {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}

export function uniqueEmail(prefix = 'e2e') {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.local`;
}

/** Password that satisfies server rules (7+ chars, uppercase, digit). */
export const E2E_VALID_PASSWORD = 'Password1';

/** Valid Alien RPG scientist build (10 skill points). */
export const scientistCharacterBuild = {
  classKey: 'scientist',
  skillAllocations: {
    observation: 2,
    survival: 2,
    comtech: 3,
    medicalAid: 3,
  },
  startingItemKey: 'medkit',
};
