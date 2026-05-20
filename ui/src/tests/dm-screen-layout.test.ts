import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

interface CssDeclarations {
  [property: string]: string;
}

const cssPath = fileURLToPath(new URL('../assets/css/app.css', import.meta.url));
const appCss = readFileSync(cssPath, 'utf8');
const portraitLayoutCss = extractRuleBody(appCss, '@media (max-width: 1023px), (orientation: portrait)');

function extractRuleBody(css: string, selector: string): string {
  const selectorIndex = css.indexOf(selector);

  expect(selectorIndex).toBeGreaterThanOrEqual(0);

  const openingBraceIndex = css.indexOf('{', selectorIndex);

  expect(openingBraceIndex).toBeGreaterThanOrEqual(0);

  let depth = 0;

  for (let index = openingBraceIndex; index < css.length; index += 1) {
    if (css[index] === '{') {
      depth += 1;
    }

    if (css[index] === '}') {
      depth -= 1;

      if (depth === 0) {
        return css.slice(openingBraceIndex + 1, index);
      }
    }
  }

  throw new Error(`Rule body for "${selector}" was not closed.`);
}

function normalizeSelector(selector: string): string {
  return selector.replace(/\s+/g, ' ').trim();
}

function parseDeclarations(block: string): CssDeclarations {
  return block
    .split(';')
    .map(declaration => declaration.trim())
    .filter(Boolean)
    .reduce<CssDeclarations>((declarations, declaration) => {
      const separatorIndex = declaration.indexOf(':');

      expect(separatorIndex).toBeGreaterThan(0);

      const property = declaration.slice(0, separatorIndex).trim();
      const value = declaration.slice(separatorIndex + 1).trim();

      declarations[property] = value;

      return declarations;
    }, {});
}

function declarationsForRule(css: string, selector: string): CssDeclarations {
  const normalizedSelector = normalizeSelector(selector);
  const rulePattern = /([^{}]+)\{([^{}]+)\}/g;
  let match: RegExpExecArray | null;

  while ((match = rulePattern.exec(css)) !== null) {
    if (normalizeSelector(match[1]) === normalizedSelector) {
      return parseDeclarations(match[2]);
    }
  }

  throw new Error(`CSS rule for "${selector}" was not found.`);
}

function expectRuleToInclude(css: string, selector: string, expectedDeclarations: CssDeclarations): void {
  expect(declarationsForRule(css, selector)).toMatchObject(expectedDeclarations);
}

describe('DM and session portrait layout CSS', () => {
  it('lets app shells grow instead of clipping lower panels in portrait and narrow layouts', () => {
    expectRuleToInclude(portraitLayoutCss, '.dm-app-shell, .app-shell:has(> .session-screen-main)', {
      'grid-template-rows': 'auto auto',
      height: 'auto',
      'min-height': '100dvh',
      overflow: 'visible',
    });
  });

  it('removes fixed-height constraints from DM and session dashboard containers', () => {
    expectRuleToInclude(portraitLayoutCss, '.dm-screen-main', {
      'grid-template-rows': 'auto auto minmax(0, auto)',
      'min-height': 'auto',
      height: 'auto',
      'max-height': 'none',
      overflow: 'visible',
    });

    expectRuleToInclude(portraitLayoutCss, '.session-screen-main', {
      'grid-template-rows': 'auto minmax(0, auto)',
      'min-height': 'auto',
      height: 'auto',
      'max-height': 'none',
      overflow: 'visible',
    });

    expectRuleToInclude(portraitLayoutCss, '.dm-screen-main > .session-dashboard-grid, .session-screen-main > .session-dashboard-grid', {
      overflow: 'visible',
      'min-height': 'auto',
      height: 'auto',
    });
  });
});
