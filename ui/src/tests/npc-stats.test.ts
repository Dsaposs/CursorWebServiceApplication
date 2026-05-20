import { describe, expect, it } from 'vitest';
import {
  npcAttributeValue,
  npcHasStructuredStats,
  npcSkillValue,
  parseNpcStatBlock,
} from '~/utils/npcStats';

describe('npc stat utilities', () => {
  it('reads structured attribute and skill values without dropping zeroes', () => {
    const statBlockJson = JSON.stringify({
      attributes: {
        strength: 4,
        agility: 0,
      },
      skills: {
        stealth: 2,
        lore: 0,
      },
    });

    expect(npcHasStructuredStats(statBlockJson)).toBe(true);
    expect(npcAttributeValue(statBlockJson, 'strength')).toBe(4);
    expect(npcAttributeValue(statBlockJson, 'agility')).toBe(0);
    expect(npcSkillValue(statBlockJson, 'stealth')).toBe(2);
    expect(npcSkillValue(statBlockJson, 'lore')).toBe(0);
  });

  it('returns null for missing values and invalid stat block JSON', () => {
    expect(parseNpcStatBlock('not json')).toBeNull();
    expect(parseNpcStatBlock('42')).toBeNull();
    expect(parseNpcStatBlock(null)).toBeNull();
    expect(npcHasStructuredStats('{"inventory":[{"itemKey":"torch","quantity":1}]}')).toBe(false);
    expect(npcAttributeValue('{"attributes":{"strength":4}}', 'agility')).toBeNull();
    expect(npcSkillValue('{"skills":{"stealth":2}}', 'lore')).toBeNull();
  });
});
