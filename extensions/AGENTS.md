# extensions/

Optional Godot-MCP packages. Each `Godot-AI-<Feature>/` owns its source-only NuGet
package, version, license, xUnit tests and live manifest. Every extension is an RQL module
(`Editor/Rql/`, `IRqlModule`) with no MCP tools of its own; see `../docs/rql.md`.

- `manifest.json` — explicit module inventory; no automatic discovery of arbitrary folders.
- `Godot-AI-*/extension.json` — identity, source provenance and pinned test-addon metadata.
- `Godot-AI-*/src/` — package source and NuGet build props; no dependency on another extension.
- `Godot-AI-*/tests/` — xUnit projects and the live manifest (`e2e/e2e-tools.json`: every guide example; driver
  `../scripts/extension_e2e.py`).
- `Godot-AI-*/testbed/` — isolated consumer fixture; not the combined live project.
- `Shared/Tests/` — test sources every extension test project links (the guide checks); never packaged.

Never add module sources to the core assembly. Compilation of a consumer containing
core plus exactly one packed extension must succeed independently of the combined
live-editor test. A combined build alone cannot prove dependency isolation.

Keep package IDs and historical NuGet versions stable. Publish only selected changed
versions. GodotSharp and third-party addons must not enter package dependencies.
Shared tests and release commands are documented in `../docs/EXTENSION-MONOREPO.md`.
