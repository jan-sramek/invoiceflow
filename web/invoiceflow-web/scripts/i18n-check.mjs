// Fails when the translation files do not have exactly the same keys.
// Usage: node scripts/i18n-check.mjs [dir]   (default: public/i18n)
import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const dir = resolve(process.argv[2] ?? 'public/i18n');
const reference = 'en';

/** Flattens nested JSON to dotted keys: { a: { b: "x" } } -> ["a.b"]. Empty strings are reported as problems. */
function flatten(value, prefix = '', keys = [], empty = []) {
  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (child !== null && typeof child === 'object' && !Array.isArray(child)) {
      flatten(child, path, keys, empty);
    } else {
      keys.push(path);
      if (typeof child !== 'string' || child.trim() === '') {
        empty.push(path);
      }
    }
  }
  return { keys, empty };
}

/** The {{ placeholders }} of a message, so a translation cannot silently drop a parameter. */
function placeholders(text) {
  return [...text.matchAll(/{{\s*([\w.]+)\s*}}/g)].map((m) => m[1]).sort();
}

function flatValues(value, prefix = '', out = new Map()) {
  for (const [key, child] of Object.entries(value)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (child !== null && typeof child === 'object') {
      flatValues(child, path, out);
    } else {
      out.set(path, String(child));
    }
  }
  return out;
}

const files = readdirSync(dir).filter((name) => name.endsWith('.json'));
const languages = files.map((name) => name.replace(/\.json$/, ''));

if (!languages.includes(reference)) {
  console.error(`i18n:check: ${join(dir, reference + '.json')} is missing.`);
  process.exit(1);
}

const data = Object.fromEntries(
  languages.map((lang) => [lang, JSON.parse(readFileSync(join(dir, `${lang}.json`), 'utf8'))]),
);
const flat = Object.fromEntries(languages.map((lang) => [lang, flatten(data[lang])]));
const values = Object.fromEntries(languages.map((lang) => [lang, flatValues(data[lang])]));

let problems = 0;
const fail = (message) => {
  problems++;
  console.error(message);
};

for (const lang of languages) {
  if (lang === reference) continue;
  const base = new Set(flat[reference].keys);
  const other = new Set(flat[lang].keys);

  for (const key of base) {
    if (!other.has(key)) fail(`${lang}.json is missing "${key}"`);
  }
  for (const key of other) {
    if (!base.has(key)) fail(`${lang}.json has "${key}" which ${reference}.json does not`);
  }
  for (const key of base) {
    if (!other.has(key)) continue;
    const expected = placeholders(values[reference].get(key)).join(',');
    const actual = placeholders(values[lang].get(key)).join(',');
    if (expected !== actual) {
      fail(`"${key}" uses {{ ${actual} }} in ${lang}.json but {{ ${expected} }} in ${reference}.json`);
    }
  }
}

for (const lang of languages) {
  for (const key of flat[lang].empty) fail(`${lang}.json has an empty value at "${key}"`);
}

if (problems > 0) {
  console.error(`\ni18n:check failed with ${problems} problem${problems === 1 ? '' : 's'}.`);
  process.exit(1);
}

console.log(`i18n:check ok: ${languages.join(', ')} share ${flat[reference].keys.length} keys.`);
