from pathlib import Path

p = Path(__file__).resolve().parents[1] / "ui/src/pages/sessions/[id]/dm.vue"
t = p.read_text(encoding="utf-8")
start = t.index("                  <!-- Roll Adjuster:")
end = t.index("                  <label>", start)
replacement = """                  <DmRollAdjuster
                    :roller-key="sessionDiceRollerKey"
                    :description="action.description"
                    :modifier="rollModifier[action.id] ?? 0"
                    @update:modifier="rollModifier[action.id] = $event; syncRollSummary(action.id)"
                  />

"""
p.write_text(t[:start] + replacement + t[end:], encoding="utf-8")
print("patched dm.vue")
