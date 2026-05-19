from pathlib import Path

root = Path(__file__).resolve().parents[1]
src = (root / "ui/src/components/DiceRoller.vue").read_text(encoding="utf-8")
template_start = src.index("<template>")
style_start = src.index("<style scoped>")
template = src[template_start:style_start]
template = template.replace("stressDiceCount && stressDiceCount > 0", "stressDiceCount > 0")
template = template.replace('placeholder="# of 6s…"', ':placeholder="`# of ${successTarget}s…`"')
template = template.replace(
    "    </p>\n\n    <!-- AUTO ROLL panel -->",
    "    </p>\n    <p v-if=\"successRule\" class=\"dr-success-hint\">{{ successRule }}</p>\n\n    <!-- AUTO ROLL panel -->",
    1,
)
import re
template = re.sub(
    r"\n        <div v-else-if=\"!isPoolMode\" class=\"dr-result-line\">.*?</motion.div>\n",
    "\n",
    template,
    flags=re.S,
)
template = re.sub(r"\n      <template v-else>.*?</template>\n", "\n", template, flags=re.S)
template = template.replace("      <template v-if=\"isPoolMode\">", "")
template = template.replace("      </template>\n\n      <!-- Modifier (manual mode", "\n      <!-- Modifier (manual mode")
template = template.replace("(manualSuccesses || manualTotal)", "manualSuccesses")

script = '''<script setup lang="ts">
import type { D6PoolRollConfig } from '~/dice-rollers/types';
import { classifyRolls, rollDice } from '~/utils/dice';
import { useDiceMode } from '~/composables/useDiceMode';

interface Props {
  config: D6PoolRollConfig;
  poolBreakdown?: string[];
  label?: string;
  successRule?: string;
  modelValue?: string;
  showModifier?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  poolBreakdown: () => [],
  label: 'Dice Roll',
  modelValue: '',
  showModifier: false,
});

const emit = defineEmits<{ 'update:modelValue': [value: string] }>();
const { mode, setMode } = useDiceMode();

const sides = computed(() => props.config.sides);
const successTarget = computed(() => props.config.successTarget);
const stressDiceCount = computed(() => props.config.stressDiceCount ?? 0);
const totalBaseDice = computed(() => Math.max(1, props.config.baseDiceCount));
const totalDice = computed(() => totalBaseDice.value + stressDiceCount.value);

const baseRolls = ref<number[]>([]);
const stressRolls = ref<number[]>([]);
const hasRolled = ref(false);
const manualSuccesses = ref('');
const modifier = ref(0);

const classification = computed(() =>
  hasRolled.value ? classifyRolls(baseRolls.value, stressRolls.value, successTarget.value) : null,
);
const adjustedSuccesses = computed(() =>
  classification.value ? classification.value.totalSuccesses + modifier.value : 0,
);

watch(() => [props.config.baseDiceCount, props.config.stressDiceCount, props.config.successTarget], clear);
watch(mode, clear);

function clear() {
  baseRolls.value = [];
  stressRolls.value = [];
  hasRolled.value = false;
  manualSuccesses.value = '';
  modifier.value = 0;
  emit('update:modelValue', '');
}

function autoRoll() {
  baseRolls.value = rollDice(totalBaseDice.value, sides.value);
  stressRolls.value = stressDiceCount.value > 0 ? rollDice(stressDiceCount.value, sides.value) : [];
  hasRolled.value = true;
  emit('update:modelValue', buildResultString());
}

function buildResultString(): string {
  if (!classification.value) return '';
  const { totalSuccesses, panicDice } = classification.value;
  const panic = panicDice.length ? ` ⚠️ PANIC (${panicDice.length} stress 1s)` : '';
  const stressNote = stressRolls.value.length
    ? ` [base: ${baseRolls.value.join(',')} | stress: ${stressRolls.value.join(',')}]`
    : ` [${[...baseRolls.value, ...stressRolls.value].join(', ')}]`;
  const adj = adjustedSuccesses.value;
  const modNote = modifier.value !== 0
    ? `${totalSuccesses} + ${modifier.value} modifier = ${adj} success${adj !== 1 ? 'es' : ''}`
    : `${totalSuccesses} success${totalSuccesses !== 1 ? 'es' : ''}`;
  return `${totalBaseDice.value + stressDiceCount.value}d${sides.value}${stressNote} → ${modNote}${panic}`;
}

watch(modifier, () => {
  if (hasRolled.value) emit('update:modelValue', buildResultString());
  else if (manualSuccesses.value) onManualChange();
});

function onManualChange() {
  const n = parseInt(manualSuccesses.value, 10);
  if (isNaN(n) || n < 0) {
    emit('update:modelValue', '');
    return;
  }
  const adj = n + modifier.value;
  const modNote = modifier.value !== 0 ? ` + ${modifier.value} modifier = ${adj}` : '';
  emit('update:modelValue', `${totalDice.value}d${sides.value}: ${n}${modNote} success${adj !== 1 ? 'es' : ''} (manual)`);
}

function isSuccess(value: number) {
  return value >= successTarget.value;
}

function isPanic(value: number, isStress: boolean) {
  return isStress && value === 1;
}
</script>

'''

out = script + template + '\n<style src="~/assets/css/dice-roller.css"></style>\n'
(root / "ui/src/dice-rollers/d6-pool/D6PoolRoller.vue").write_text(out, encoding="utf-8")
print("wrote D6PoolRoller.vue", len(out))
