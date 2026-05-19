from pathlib import Path
import re

p = Path(__file__).resolve().parents[1] / "ui/src/dice-rollers/d6-pool/D6PoolRoller.vue"
text = p.read_text(encoding="utf-8")

text = text.replace(
    "import { classifyRolls, parseDiceNotation, rollDice }",
    "import type { D6PoolRollConfig } from '~/dice-rollers/types';\nimport { classifyRolls, rollDice }",
)
text = re.sub(
    r"interface Props \{[^}]+\}\n\nconst props = withDefaults\(defineProps<Props>\(\), \{[^}]+\}\);",
    """interface Props {
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
});""",
    text,
    count=1,
    flags=re.S,
)

replacements = [
    ("const totalBaseDice = computed(() => Math.max(1, props.baseDiceCount));",
     "const sides = computed(() => props.config.sides);\nconst successTarget = computed(() => props.config.successTarget);\nconst stressDiceCount = computed(() => props.config.stressDiceCount ?? 0);\nconst totalBaseDice = computed(() => Math.max(1, props.config.baseDiceCount));"),
    ("const totalDice = computed(() => totalBaseDice.value + (props.stressDiceCount ?? 0));",
     "const totalDice = computed(() => totalBaseDice.value + stressDiceCount.value);"),
    ("const isPoolMode = computed(() => props.successTarget !== undefined && props.successTarget > 0);",
     "const isPoolMode = computed(() => true);"),
    ("props.successTarget!", "successTarget.value"),
    ("watch([() => props.baseDiceCount, () => props.stressDiceCount, () => props.successTarget], clear);",
     "watch(() => [props.config.baseDiceCount, props.config.stressDiceCount, props.config.successTarget], clear);"),
    ("rollDice(totalBaseDice.value, props.sides)", "rollDice(totalBaseDice.value, sides.value)"),
    ("(props.stressDiceCount ?? 0) > 0\n    ? rollDice(props.stressDiceCount!, props.sides)",
     "stressDiceCount.value > 0\n    ? rollDice(stressDiceCount.value, sides.value)"),
    ("totalBaseDice.value + (props.stressDiceCount ?? 0)}d${props.sides}",
     "totalBaseDice.value + stressDiceCount.value}d${sides.value}"),
    ("props.successTarget !== undefined && value >= props.successTarget",
     "value >= successTarget.value"),
    ('placeholder="# of 6s…"', ':placeholder="`# of ${successTarget}s…`"'),
    ("stressDiceCount && stressDiceCount > 0", "stressDiceCount > 0"),
    ("(manualSuccesses || manualTotal)", "manualSuccesses"),
]
for a, b in replacements:
    text = text.replace(a, b)

text = re.sub(r"\nconst sumTotal = computed.*?\n\n", "\n", text, flags=re.S)
text = re.sub(r"\nconst adjustedTotal = computed.*?\n", "\n", text, flags=re.S)
text = re.sub(r"  const manualTotal = ref.*?;\n", "", text)
text = text.replace("  manualTotal.value = '';\n  ", "")
text = re.sub(r"  } else \{\n    const n = parseInt\(manualTotal\.value.*?\n  \}\n", "", text, flags=re.S)
text = re.sub(
    r"  const adj = adjustedTotal\.value;.*?return `\$\{totalDice\.value\}d\$\{props\.sides\}: \$\{modNote\}`;\n",
    '  return "";\n',
    text,
    flags=re.S,
)
text = re.sub(
    r'        <motion.div v-else-if="!isPoolMode" class="dr-result-line">.*?</motion.div>\n\n',
    "",
    text,
    flags=re.S,
)
text = re.sub(
    r"      <template v-else>.*?</template>\n\n",
    "",
    text,
    flags=re.S,
)
text = text.replace("      <template v-if=\"isPoolMode\">", "")
text = text.replace(
    "    </p>\n\n    <!-- AUTO ROLL panel -->",
    "    </p>\n    <p v-if=\"successRule\" class=\"dr-success-hint\">{{ successRule }}</p>\n\n    <!-- AUTO ROLL panel -->",
    1,
)

p.write_text(text, encoding="utf-8")
print("patched", p)
