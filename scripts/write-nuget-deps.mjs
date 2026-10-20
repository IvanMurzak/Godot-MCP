#!/usr/bin/env node
// Generate `addons/godot_mcp/nuget-deps.json` from Godot-MCP.csproj at release time.
//
// The addon ships as source, so a consumer's .csproj must declare the same NuGet
// PackageReferences the addon was built against. `godot-cli install-plugin` reads this
// file from the addon it installs (instead of the pins baked into its own build), so the
// consumer is pinned to what THAT addon release needs. The file is GENERATED (and
// gitignored) — Godot-MCP.csproj stays the single source of truth; never hand-edit it.
//
// Usage: node scripts/write-nuget-deps.mjs [outputPath]
//   outputPath defaults to addons/godot_mcp/nuget-deps.json (relative to the repo root).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const csprojPath = path.join(repoRoot, 'Godot-MCP.csproj');
const outputPath = path.resolve(repoRoot, process.argv[2] ?? 'addons/godot_mcp/nuget-deps.json');

// Same strings as EDITOR_ONLY_CONDITION / CODE_EXEC_IN_GAME_CONDITION in cli/src/utils/addon-deps.ts (the generator
// test reads the manifest back).
const EDITOR_ONLY_CONDITION = "'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'";
const CODE_EXEC_IN_GAME_CONDITION = `'$(GodotMcpCodeExecInGame)' == 'true' or (${EDITOR_ONLY_CONDITION})`;

const csproj = fs.readFileSync(csprojPath, 'utf-8');
const packageReferences = [];
for (const element of csproj.match(/<PackageReference\b[^>]*>/gi) ?? []) {
  const id = /\bInclude\s*=\s*"([^"]+)"/i.exec(element)?.[1];
  const version = /\bVersion\s*=\s*"([^"]+)"/i.exec(element)?.[1];
  // The editor-only Condition marks a package exports leave out, the in-game one a package exports keep when the game
  // opts in to in-game script-execute (`inGameOptIn`, which an older godot-cli ignores: it writes plain editor-only);
  // godot-cli writes the same condition. Any other Condition is refused: the manifest could not carry it.
  const condition = /\bCondition\s*=\s*"([^"]*)"/i.exec(element)?.[1];
  if (condition !== undefined && condition !== EDITOR_ONLY_CONDITION && condition !== CODE_EXEC_IN_GAME_CONDITION) {
    console.error(
      `Unsupported Condition in ${csprojPath} (only "${EDITOR_ONLY_CONDITION}" or "${CODE_EXEC_IN_GAME_CONDITION}"): ${element}`,
    );
    process.exit(1);
  }
  const editorOnly = condition !== undefined;
  const inGameOptIn = condition === CODE_EXEC_IN_GAME_CONDITION;
  if (id !== undefined && version !== undefined) {
    packageReferences.push({ id, version, ...(editorOnly && { editorOnly }), ...(inGameOptIn && { inGameOptIn }) });
  }
}

if (packageReferences.length === 0) {
  console.error(`No <PackageReference Include=".." Version=".."> found in ${csprojPath}`);
  process.exit(1);
}

const manifest = { schemaVersion: 1, packageReferences };
fs.writeFileSync(outputPath, `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`Wrote ${path.relative(repoRoot, outputPath)}:`);
for (const ref of packageReferences) {
  const scope = ref.inGameOptIn ? ' (editor-only; in game with GodotMcpCodeExecInGame)' : ref.editorOnly ? ' (editor-only)' : '';
  console.log(`  ${ref.id} ${ref.version}${scope}`);
}
