// Minimal JUnit XML reader for the reports GUT and gdUnit4 write (no XML dependency). Pure.

export interface JUnitCase {
  suite: string;
  name: string;
  classname?: string;
  file?: string;
  status: 'passed' | 'failed' | 'error' | 'skipped';
  message?: string;
  /** Failure / error body text. */
  details?: string;
  timeSeconds?: number;
}

export interface JUnitReport {
  tests: number;
  passed: number;
  failures: number;
  errors: number;
  skipped: number;
  cases: JUnitCase[];
}

const TAG_RE = /<(\/?)(testsuites|testsuite|testcase|failure|error|skipped)\b((?:[^>"']|"[^"]*"|'[^']*')*?)(\/?)>/g;
const ATTR_RE = /([A-Za-z_:][\w:.-]*)\s*=\s*("([^"]*)"|'([^']*)')/g;

export function decodeXml(text: string): string {
  return text
    .replace(/<!\[CDATA\[([\s\S]*?)\]\]>/g, '$1')
    .replace(/&#x([0-9a-f]+);/gi, (_, hex: string) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, dec: string) => String.fromCodePoint(Number(dec)))
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&');
}

function attrs(raw: string): Record<string, string> {
  const out: Record<string, string> = {};
  for (const m of raw.matchAll(ATTR_RE)) out[m[1]] = decodeXml(m[3] ?? m[4] ?? '');
  return out;
}

/** Parse a JUnit XML document. Throws when it holds no `<testsuite>` / `<testsuites>` element. */
export function parseJUnit(xml: string): JUnitReport {
  const cases: JUnitCase[] = [];
  const suites: string[] = [];
  let sawRoot = false;
  let current: JUnitCase | undefined;
  let outcomeStart = -1;
  let outcomeTag = '';

  for (const m of xml.matchAll(TAG_RE)) {
    const [whole, closing, tag, rawAttrs, selfClosing] = m;
    const at = m.index ?? 0;
    if (tag === 'testsuites') {
      sawRoot = true;
      continue;
    }
    if (tag === 'testsuite') {
      sawRoot = true;
      if (closing) suites.pop();
      else if (!selfClosing) suites.push(attrs(rawAttrs).name ?? '');
      continue;
    }
    if (tag === 'testcase') {
      if (closing) {
        current = undefined;
        continue;
      }
      const a = attrs(rawAttrs);
      const time = a.time !== undefined ? Number(a.time) : undefined;
      current = {
        suite: suites[suites.length - 1] ?? '',
        name: a.name ?? '',
        ...(a.classname ? { classname: a.classname } : {}),
        ...(a.file ? { file: a.file } : {}),
        status: 'passed',
        ...(time !== undefined && Number.isFinite(time) ? { timeSeconds: time } : {}),
      };
      cases.push(current);
      if (selfClosing) current = undefined;
      continue;
    }
    // failure / error / skipped
    if (!current) continue;
    if (closing) {
      if (tag === outcomeTag && outcomeStart >= 0) {
        const body = decodeXml(xml.slice(outcomeStart, at)).trim();
        if (body) current.details = body;
      }
      outcomeStart = -1;
      continue;
    }
    const a = attrs(rawAttrs);
    const status = tag === 'failure' ? 'failed' : tag === 'error' ? 'error' : 'skipped';
    // A failure outranks a skip on the same case.
    if (current.status === 'passed' || status !== 'skipped') current.status = status;
    if (a.message) current.message = a.message;
    if (!selfClosing) {
      outcomeTag = tag;
      outcomeStart = at + whole.length;
    }
  }

  if (!sawRoot) throw new Error('not a JUnit report (no <testsuite> element)');
  const count = (s: JUnitCase['status']) => cases.filter((c) => c.status === s).length;
  return {
    tests: cases.length,
    passed: count('passed'),
    failures: count('failed'),
    errors: count('error'),
    skipped: count('skipped'),
    cases,
  };
}

/** A failing case's line number from its message/details (`at line 12`, `line: 12`, `:12`). */
export function failureLine(c: JUnitCase): number | undefined {
  const text = `${c.message ?? ''}\n${c.details ?? ''}`;
  const m = /\bat line (\d+)|\bline[:\s]+(\d+)|\.gd:(\d+)/i.exec(text);
  const n = m ? Number(m[1] ?? m[2] ?? m[3]) : NaN;
  return Number.isFinite(n) ? n : undefined;
}

/**
 * The `res://…gd` file a case belongs to: its `file`, classname or suite, when one of them is a script path (GUT
 * writes project-relative `test/test_x.gd`, gdUnit4 `res://…`).
 */
export function caseScript(c: JUnitCase): string | undefined {
  for (const candidate of [c.file, c.classname, c.suite]) {
    if (!candidate) continue;
    const res = /res:\/\/\S+?\.gd/.exec(candidate);
    if (res) return res[0];
    if (/^[\w.\- /]+\.gd$/.test(candidate) && !candidate.startsWith('/')) return `res://${candidate}`;
  }
  // gdUnit4 names the file only in the failure (`FAILED: res://test/math_test.gd:7`).
  return /res:\/\/[^\s:'"]+?\.gd/.exec(`${c.message ?? ''}\n${c.details ?? ''}`)?.[0];
}

/** The most useful one-line description of a failed case: the report body when the message is generic. */
export function failureSummary(c: JUnitCase): string {
  const detail = c.details?.replace(/\s+/g, ' ').trim().slice(0, 300) || undefined;
  if (detail && (!c.message || /^(failed|failure|error)(:\s*\S+)?$/i.test(c.message.trim()))) return detail;
  return c.message || detail || c.status;
}
