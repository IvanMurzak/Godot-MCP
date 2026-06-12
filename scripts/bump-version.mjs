#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptPath = fileURLToPath(import.meta.url);
const repoRoot = path.resolve(path.dirname(scriptPath), '..');
const colorEnabled = !process.env.NO_COLOR && process.env.TERM !== 'dumb';
const color = {
  bold: (text) => paint(text, '1'),
  dim: (text) => paint(text, '2'),
  red: (text) => paint(text, '31'),
  green: (text) => paint(text, '32'),
  yellow: (text) => paint(text, '33'),
  cyan: (text) => paint(text, '36'),
};

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const help = args.includes('--help') || args.includes('-h');
const positional = args.filter((arg) => !arg.startsWith('-'));
const unknownFlags = args.filter((arg) => arg.startsWith('-') && !['--dry-run', '--help', '-h'].includes(arg));

if (unknownFlags.length > 0) {
  fail(`Unknown option(s): ${unknownFlags.join(', ')}`);
}

if (help || positional.length !== 1) {
  printUsage();
  process.exit(help ? 0 : 1);
}

const nextVersion = positional[0];
const semverPattern = /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/;

if (!semverPattern.test(nextVersion)) {
  fail(`Invalid version "${nextVersion}". Expected SemVer like 0.6.0 or 0.6.0-beta.1.`);
}

const pluginCfgPath = 'addons/godot_mcp/plugin.cfg';
const currentVersion = readPluginVersion(pluginCfgPath);
const changes = [
  updatePluginCfg(pluginCfgPath, nextVersion),
  updateJsonVersion('cli/package.json', nextVersion),
  updatePackageLock('cli/package-lock.json', nextVersion),
  updateFallbackVersion('addons/godot_mcp/Connection/GodotMcpConnection.cs', nextVersion),
  updateAssetLibrarySubmission('docs/assetlib/SUBMISSION.md', nextVersion),
].filter(Boolean);

if (changes.length === 0) {
  console.log(`${color.green('ok')} Godot-MCP is already at version ${color.bold(nextVersion)}.`);
  process.exit(0);
}

console.log(
  `${dryRun ? color.yellow('Dry run: would bump') : color.green('Bumping')} ` +
    `Godot-MCP ${color.red(currentVersion)} -> ${color.green(nextVersion)}`,
);

for (const change of changes) {
  if (dryRun) {
    console.log('');
    console.log(diffText(change.relativePath, change.before, change.after));
  } else {
    fs.writeFileSync(abs(change.relativePath), change.after);
    console.log(`${color.green('updated')} ${color.cyan(change.relativePath)}`);
  }
}

if (dryRun) {
  console.log('');
  console.log(color.dim('No files were changed.'));
}

function printUsage() {
  console.log(`Usage:
  node scripts/bump-version.mjs <version> [--dry-run]

Examples:
  node scripts/bump-version.mjs 0.6.0 --dry-run
  node scripts/bump-version.mjs 0.6.0`);
}

function updatePluginCfg(relativePath, version) {
  return updateTextFile(relativePath, (text) =>
    replaceExactly(
      text,
      /^version="[^"]+"$/m,
      `version="${version}"`,
      relativePath,
      'plugin.cfg version',
    ),
  );
}

function updateJsonVersion(relativePath, version) {
  const before = read(relativePath);
  const data = JSON.parse(before);
  data.version = version;
  const after = jsonStringifyLike(before, data);
  return makeChange(relativePath, before, after);
}

function updatePackageLock(relativePath, version) {
  const before = read(relativePath);
  const data = JSON.parse(before);
  data.version = version;

  if (!data.packages || !data.packages['']) {
    fail(`${relativePath}: expected packages[""] in package-lock.json.`);
  }

  data.packages[''].version = version;
  const after = jsonStringifyLike(before, data);
  return makeChange(relativePath, before, after);
}

function updateFallbackVersion(relativePath, version) {
  return updateTextFile(relativePath, (text) =>
    replaceExactly(
      text,
      /const string FallbackPluginVersion = "[^"]+";/,
      `const string FallbackPluginVersion = "${version}";`,
      relativePath,
      'FallbackPluginVersion',
    ),
  );
}

function updateAssetLibrarySubmission(relativePath, version) {
  return updateTextFile(relativePath, (text) => {
    let next = replaceExactly(
      text,
      /\| \*\*Version\*\* \| `[^`]+` \(must match `addons\/godot_mcp\/plugin\.cfg` `version=` and the `v[^`]+` tag\) \|/,
      `| **Version** | \`${version}\` (must match \`addons/godot_mcp/plugin.cfg\` \`version=\` and the \`v${version}\` tag) |`,
      relativePath,
      'Asset Library Version row',
    );

    next = replaceExactly(
      next,
      /\| \*\*Download Commit\*\* \| the commit hash the `v[^`]+` tag points at \u2014 get it with `git rev-list -n1 v[^`]+` \(a hash, not the tag name\) \|/,
      `| **Download Commit** | the commit hash the \`v${version}\` tag points at \u2014 get it with \`git rev-list -n1 v${version}\` (a hash, not the tag name) |`,
      relativePath,
      'Asset Library Download Commit row',
    );

    return next;
  });
}

function updateTextFile(relativePath, updater) {
  const before = read(relativePath);
  const after = updater(before);
  return makeChange(relativePath, before, after);
}

function replaceExactly(text, pattern, replacement, relativePath, label) {
  const matches = text.match(new RegExp(pattern.source, pattern.flags.includes('g') ? pattern.flags : `${pattern.flags}g`));

  if (!matches || matches.length !== 1) {
    fail(`${relativePath}: expected exactly one ${label} match, found ${matches ? matches.length : 0}.`);
  }

  return text.replace(pattern, replacement);
}

function readPluginVersion(relativePath) {
  const text = read(relativePath);
  const match = text.match(/^version="([^"]+)"$/m);

  if (!match) {
    fail(`${relativePath}: could not read version.`);
  }

  return match[1];
}

function makeChange(relativePath, before, after) {
  if (before === after) {
    return null;
  }

  return { relativePath, before, after };
}

function read(relativePath) {
  return fs.readFileSync(abs(relativePath), 'utf8');
}

function jsonStringifyLike(originalText, data) {
  const eol = originalText.includes('\r\n') ? '\r\n' : '\n';
  return `${JSON.stringify(data, null, 2)}\n`.replace(/\n/g, eol);
}

function abs(relativePath) {
  return path.join(repoRoot, relativePath);
}

function fail(message) {
  console.error(`${color.red('bump-version:')} ${message}`);
  process.exit(1);
}

function paint(text, code) {
  return colorEnabled ? `\x1b[${code}m${text}\x1b[0m` : text;
}

function diffText(relativePath, before, after) {
  const beforeLines = before.split(/\r?\n/);
  const afterLines = after.split(/\r?\n/);
  const max = Math.max(beforeLines.length, afterLines.length);
  const changed = [];

  for (let i = 0; i < max; i += 1) {
    if (beforeLines[i] !== afterLines[i]) {
      changed.push(i);
    }
  }

  if (changed.length === 0) {
    return `${color.bold(`diff -- ${relativePath}`)}\n${color.dim('(no changes)')}`;
  }

  const ranges = [];
  let start = Math.max(0, changed[0] - 2);
  let end = Math.min(max - 1, changed[0] + 2);

  for (const line of changed.slice(1)) {
    const lineStart = Math.max(0, line - 2);
    const lineEnd = Math.min(max - 1, line + 2);

    if (lineStart <= end + 1) {
      end = Math.max(end, lineEnd);
    } else {
      ranges.push([start, end]);
      start = lineStart;
      end = lineEnd;
    }
  }

  ranges.push([start, end]);

  const output = [
    color.bold(`diff -- ${relativePath}`),
    color.red(`--- ${relativePath}`),
    color.green(`+++ ${relativePath}`),
  ];

  for (const [rangeStart, rangeEnd] of ranges) {
    output.push(color.cyan(`@@ ${rangeStart + 1},${rangeEnd - rangeStart + 1} @@`));

    for (let i = rangeStart; i <= rangeEnd; i += 1) {
      const oldLine = beforeLines[i];
      const newLine = afterLines[i];

      if (oldLine === newLine) {
        output.push(color.dim(` ${oldLine ?? ''}`));
      } else {
        if (oldLine !== undefined) {
          output.push(color.red(`-${oldLine}`));
        }
        if (newLine !== undefined) {
          output.push(color.green(`+${newLine}`));
        }
      }
    }
  }

  return output.join('\n');
}
