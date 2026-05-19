import type { RulesetResponse, RulesetTheme } from '~/types/api';

/** Maps RulesetTheme keys to CSS custom property names used in app.css. */
const CSS_VAR_MAP: Record<keyof Omit<RulesetTheme, 'fontFamily'>, string> = {
  bg: '--bg',
  surface: '--surface',
  panel: '--panel',
  panelAlt: '--panel-alt',
  panelHover: '--panel-hover',
  border: '--border',
  borderStrong: '--border-strong',
  ink: '--ink',
  inkBright: '--ink-bright',
  muted: '--muted',
  mutedLight: '--muted-light',
  accent: '--accent',
  accentDark: '--accent-dark',
  accentDim: '--accent-dim',
  secondary: '--secondary',
  secondaryDim: '--secondary-dim',
  danger: '--danger',
  dangerDim: '--danger-dim',
  success: '--success',
  successDim: '--success-dim',
  warn: '--warn',
  warnDim: '--warn-dim',
};

/**
 * Returns a reactive style object (CSS custom property overrides) derived from
 * the ruleset's `theme` block. Apply to the root session element via `:style`.
 *
 * Returns an empty object when the ruleset has no theme or hasn't loaded yet,
 * so the default app.css variables remain in effect.
 */
export function useRulesetTheme(ruleset: Ref<RulesetResponse | null | undefined>) {
  return computed<Record<string, string>>(() => {
    const definitionJson = ruleset.value?.definitionJson;
    if (!definitionJson) return {};

    let theme: RulesetTheme | undefined;
    try {
      const parsed = JSON.parse(definitionJson) as { theme?: RulesetTheme };
      theme = parsed.theme;
    } catch {
      return {};
    }

    if (!theme) return {};

    const style: Record<string, string> = {};

    for (const [key, cssVar] of Object.entries(CSS_VAR_MAP) as [keyof typeof CSS_VAR_MAP, string][]) {
      const value = theme[key];
      if (typeof value === 'string' && value) {
        style[cssVar] = value;
      }
    }

    if (theme.fontFamily) {
      style['font-family'] = theme.fontFamily;
    }

    return style;
  });
}
