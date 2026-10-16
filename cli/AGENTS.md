# cli/ — `godot-cli` npm package

Node (`^20.19.0 || >=22.12.0`) TypeScript ESM CLI, the Godot analog of `unity-mcp-cli`: launches the
editor with `GODOT_MCP_*` env vars, calls tools over the server HTTP API, writes agent MCP config and
skills, installs/enables the addon. Published by the addon's `release.yml` at the same version (never alone).
User-facing detail: [`README.md`](README.md).

```bash
npm install && npm run build && npm test   # tsc → dist/, vitest
```

- `src/cli.ts` — commander entry; `src/commands/` — one file per command.
- `src/lib.ts` + `src/lib/` — side-effect-free library API: no top-level side effects, no `commander`
  import, results are `{ kind: 'success' | 'failure' }` unions, never throw past the boundary.
- `src/utils/` — shared helpers. `skills.ts` (`SKILL_FAMILIES`) and `addon-deps.ts` mirror the addon
  and are parity-tested against it.
- `tests/` — vitest, `<subject>.test.ts`; `*-parity.test.ts` diff CLI tables against addon sources/docs.
- New command: `src/commands/<name>.ts`; logic the library should expose goes in `src/lib/<name>.ts`,
  exported from `src/lib.ts`.
- `setup-skills` generates skills locally from `SKILL_FAMILIES` (no server/editor needed).
