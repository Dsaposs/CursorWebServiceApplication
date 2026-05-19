<script setup lang="ts">
import type { RulesetDefinition, RulesetItemDefinition } from '~/types/api';
import { parseInventory, type InventoryEntry } from '~/utils/inventory';
import { findRulesetItem } from '~/utils/rulesets';

interface Props {
  inventoryJson: string;
  rulesetDefinition?: RulesetDefinition | null;
}

const props = defineProps<Props>();

const entries = computed(() => parseInventory(props.inventoryJson));

function itemDef(entry: InventoryEntry): RulesetItemDefinition | null {
  return findRulesetItem(props.rulesetDefinition ?? null, entry.itemKey);
}

function itemLabel(entry: InventoryEntry): string {
  return itemDef(entry)?.label ?? entry.itemKey;
}

function hasDetail(entry: InventoryEntry): boolean {
  const def = itemDef(entry);
  if (!def) return false;
  return Boolean(
    def.description
    || def.category
    || def.attackRoll
    || def.damageRoll
    || (def.modifiers && def.modifiers.some(m => m.attackBonus || m.flatDice)),
  );
}

function attributeLabel(key: string): string {
  return props.rulesetDefinition?.character.attributes.find(a => a.key === key)?.label ?? key;
}

function skillLabel(key: string): string {
  return props.rulesetDefinition?.character.skills.find(s => s.key === key)?.label ?? key;
}

function diceLabel(key: string): string {
  return props.rulesetDefinition?.dice.find(d => d.key === key)?.notation ?? key;
}

function damageDescription(def: RulesetItemDefinition): string {
  const dr = def.damageRoll;
  if (!dr) return '';
  const parts = [dr.notation];
  if (dr.bonusAttribute) parts.push(`+ ${attributeLabel(dr.bonusAttribute)} mod`);
  if (dr.flatBonus) parts.push(`${dr.flatBonus >= 0 ? '+' : ''}${dr.flatBonus}`);
  return parts.join(' ');
}

function meaningfulModifiers(def: RulesetItemDefinition) {
  return (def.modifiers ?? []).filter(m => m.attackBonus || m.flatDice);
}
</script>

<template>
  <ul v-if="entries.length" class="inventory-list">
    <li v-for="entry in entries" :key="entry.itemKey" class="inventory-list-item">
      <details v-if="hasDetail(entry)" class="inventory-item-details">
        <summary class="inventory-item-summary">
          <span class="inventory-item-name">{{ itemLabel(entry) }}</span>
          <span class="inventory-item-summary-right">
            <span class="inventory-item-qty">×{{ entry.quantity }}</span>
            <span class="inventory-item-chevron" aria-hidden="true">▸</span>
          </span>
        </summary>

        <div class="inventory-item-detail-body">
          <div v-if="itemDef(entry)?.category" class="inventory-item-category">
            {{ itemDef(entry)!.category }}
          </div>

          <p v-if="itemDef(entry)?.description" class="inventory-item-description">
            {{ itemDef(entry)!.description }}
          </p>

          <div v-if="itemDef(entry)?.attackRoll" class="inventory-item-stat-block">
            <div class="inventory-stat-heading">Attack Roll</div>
            <dl class="inventory-stat-grid">
              <div class="inventory-stat-row">
                <dt>Dice</dt>
                <dd>{{ diceLabel(itemDef(entry)!.attackRoll!.dice) }}</dd>
              </div>
              <div class="inventory-stat-row">
                <dt>Pool</dt>
                <dd>
                  {{ attributeLabel(itemDef(entry)!.attackRoll!.attribute) }}
                  + {{ skillLabel(itemDef(entry)!.attackRoll!.skill) }}
                </dd>
              </div>
              <div v-if="itemDef(entry)!.attackRoll!.successRule" class="inventory-stat-row inventory-stat-row--full">
                <dt>Hit</dt>
                <dd>{{ itemDef(entry)!.attackRoll!.successRule }}</dd>
              </div>
            </dl>
          </div>

          <div v-if="itemDef(entry)?.damageRoll" class="inventory-item-stat-block">
            <div class="inventory-stat-heading">Damage</div>
            <dl class="inventory-stat-grid">
              <div class="inventory-stat-row">
                <dt>Roll</dt>
                <dd>{{ damageDescription(itemDef(entry)!) }}</dd>
              </div>
              <div v-if="itemDef(entry)!.damageRoll!.description" class="inventory-stat-row inventory-stat-row--full">
                <dt>Note</dt>
                <dd>{{ itemDef(entry)!.damageRoll!.description }}</dd>
              </div>
            </dl>
          </div>

          <div v-if="meaningfulModifiers(itemDef(entry)!).length" class="inventory-item-stat-block">
            <div class="inventory-stat-heading">Modifiers</div>
            <dl class="inventory-stat-grid">
              <div
                v-for="(mod, i) in meaningfulModifiers(itemDef(entry)!)"
                :key="i"
                class="inventory-stat-row"
              >
                <dt>{{ mod.source === 'item' ? 'Bonus' : mod.source }}</dt>
                <dd>
                  <template v-if="mod.attackBonus">+{{ mod.attackBonus }} to hit</template>
                  <template v-if="mod.flatDice">+{{ mod.flatDice }} dice</template>
                </dd>
              </div>
            </dl>
          </div>
        </div>
      </details>

      <template v-else>
        <span class="inventory-item-name">{{ itemLabel(entry) }}</span>
        <span class="inventory-item-qty">×{{ entry.quantity }}</span>
      </template>
    </li>
  </ul>
  <p v-else class="text-sm muted">No items.</p>
</template>
