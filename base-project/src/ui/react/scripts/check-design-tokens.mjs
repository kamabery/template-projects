// docs/DESIGN.md, "Never a raw value": colours, radii and shadows come from the tokens in
// src/index.css, never from Tailwind's built-in palette or scale. No compiler, linter or test knew
// that rule, so a raw `rounded-lg` shipped in the template itself. `npm run lint` runs this after
// oxlint. The style guide is exempt: its prose names the forbidden classes on purpose.
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

const root = join(import.meta.dirname, '..', 'src');
const exempt = [/routes[\\/]styleguide\.tsx$/, /\.generated\.ts$/, /routeTree\.gen\.ts$/];

const palette = 'slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose';
const raw = [
  [new RegExp(`\\b(?:bg|text|border|ring|outline|fill|stroke|from|via|to|divide|decoration|caret|accent|placeholder)-(?:${palette})-\\d{2,3}\\b`), 'a raw colour; use a colour token'],
  // A dialog's `backdrop:bg-black/40` scrim is allowed: translucent black dims either theme alike.
  [/(?<!backdrop:)\b(?:bg|text|border|ring|fill|stroke)-(?:black|white)\b/, 'a raw colour; use a colour token'],
  [/\brounded(?:-[trblse]{1,2})?-(?:xs|sm|md|lg|xl|2xl|3xl|4xl|full)\b/, 'a raw radius; use rounded-control, rounded-surface or rounded-pill'],
  [/\brounded(?:-[trblse]{1,2})?(?=["'`\s])/, 'a raw radius; use rounded-control, rounded-surface or rounded-pill'],
  [/\bshadow-(?:2xs|xs|sm|md|lg|xl|2xl|inner)\b/, 'a raw shadow; use shadow-raised or shadow-overlay'],
];

function* files(dir) {
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) yield* files(p);
    else if (/\.(tsx|ts)$/.test(e.name) && !exempt.some((x) => x.test(p))) yield p;
  }
}

const found = [];
for (const file of files(root)) {
  readFileSync(file, 'utf8').split('\n').forEach((line, i) => {
    if (/^\s*(\/\/|\*)/.test(line)) return;
    for (const [pattern, why] of raw) {
      const m = line.match(pattern);
      if (m) found.push(`${relative(join(root, '..'), file)}:${i + 1}: '${m[0]}' is ${why} (docs/DESIGN.md)`);
    }
  });
}

if (found.length) {
  console.error(found.join('\n'));
  console.error(`check-design-tokens: ${found.length} raw value(s)`);
  process.exit(1);
}
console.log('check-design-tokens: no raw colours, radii or shadows');
