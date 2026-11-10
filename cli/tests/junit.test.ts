import { describe, it, expect } from 'vitest';
import { caseScript, decodeXml, failureLine, failureSummary, parseJUnit } from '../src/lib/junit.js';

// Written by GUT 9.6.1 (-gjunit_xml_file).
const GUT_XML = `<?xml version="1.0" encoding="UTF-8"?>
<testsuites name="GutTests" failures="1" tests="3" >
  <testsuite name="test/test_failing.gd" tests="1" failures="1" skipped="0" time="0.000322" >
      <testcase name="test_fails" assertions="1" status="fail" classname="test/test_failing.gd" time="0.000322" >
      <failure message="failed"><![CDATA[[2] expected to equal [3]:  math is broken
            at line 4]]></failure></testcase>
  </testsuite>
  <testsuite name="test/test_sample.gd" tests="2" failures="0" skipped="0" time="0.000189" >
      <testcase name="test_adds" assertions="1" status="pass" classname="test/test_sample.gd" time="0.000113" >
      </testcase>
      <testcase name="test_truth" assertions="1" status="pass" classname="test/test_sample.gd" time="0.000076" >
      </testcase>
  </testsuite>
</testsuites>`;

// Written by gdUnit4 6.2.3 (-rd <dir> → report_1/results.xml).
const GDUNIT_XML = `<?xml version="1.0" encoding="UTF-8" ?>
<testsuites id="2026-11-09" name="report_1" tests="2" failures="1" skipped="0" flaky="0" time="0.000">
	<testsuite id="0" name="math_test" package="test" tests="2" failures="1" errors="0" skipped="0" time="0.091">
		<testcase name="test_adds" classname="math_test" time="0.053">
		</testcase>
		<testcase name="test_fails" classname="math_test" time="0.002">
			<failure message="FAILED: res://test/math_test.gd:7" type="FAILURE">
<![CDATA[
Expecting:
 3
 but was
 2
	at 'test_fails' in res://test/math_test.gd:7
]]>
			</failure>
		</testcase>
	</testsuite>
</testsuites>`;

describe('parseJUnit', () => {
  it('reads a GUT report', () => {
    const r = parseJUnit(GUT_XML);
    expect(r).toMatchObject({ tests: 3, passed: 2, failures: 1, errors: 0, skipped: 0 });
    const failed = r.cases.find((c) => c.status === 'failed')!;
    expect(failed).toMatchObject({ suite: 'test/test_failing.gd', name: 'test_fails', message: 'failed' });
    expect(failed.details).toContain('math is broken');
    expect(caseScript(failed)).toBe('res://test/test_failing.gd');
    expect(failureLine(failed)).toBe(4);
    expect(failureSummary(failed)).toBe('[2] expected to equal [3]: math is broken at line 4');
  });

  it('reads a gdUnit4 report (file only named in the failure)', () => {
    const r = parseJUnit(GDUNIT_XML);
    expect(r).toMatchObject({ tests: 2, passed: 1, failures: 1 });
    const failed = r.cases.find((c) => c.status === 'failed')!;
    expect(caseScript(failed)).toBe('res://test/math_test.gd');
    expect(failureLine(failed)).toBe(7);
    expect(failureSummary(failed)).toMatch(/^Expecting: 3 but was 2/);
  });

  it('handles self-closing cases, errors, skips and entities', () => {
    const r = parseJUnit(
      '<testsuite name="s"><testcase name="a &amp; b"/><testcase name="e"><error message="boom &lt;x&gt;"/></testcase>' +
        '<testcase name="k"><skipped/></testcase></testsuite>',
    );
    expect(r).toMatchObject({ tests: 3, passed: 1, errors: 1, skipped: 1 });
    expect(r.cases[0].name).toBe('a & b');
    expect(r.cases[1].message).toBe('boom <x>');
  });

  it('throws on a document without test suites', () => {
    expect(() => parseJUnit('<html></html>')).toThrow(/not a JUnit report/);
  });

  it('decodes numeric entities and CDATA', () => {
    expect(decodeXml('&#65;&#x42;<![CDATA[<c>]]>')).toBe('AB<c>');
  });
});
