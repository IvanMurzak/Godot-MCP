# Releasing Godot-MCP

How a new version of **Godot-MCP** is built, published, and distributed.

There are two distribution artifacts, both cut from the **same release version**:

1. the **addon source folder** (`addons/godot_mcp/`), shipped as
   - a **GitHub Release** with a versioned `godot-mcp-addon-<version>.zip` attached, and
   - a **Godot Asset Library** entry that points at that repository/version; and
2. the **`godot-mcp-cli`** npm package (`cli/`), published to the public npm registry.

Godot-MCP does **not** publish any NuGet package of its own — see [NuGet](#nuget-decision) below.

## Version source of truth (addon ⇄ cli reconciliation)

The release version has a **single source of truth: the `version` field in
[`addons/godot_mcp/plugin.cfg`](../addons/godot_mcp/plugin.cfg)**. That value:

- names the GitHub Release tag (`v<version>`) and the addon zip (`godot-mcp-addon-<version>.zip`);
- is written into `cli/package.json` **at publish time** by the deploy workflow via
  `npm version <version> --no-git-tag-version --allow-same-version`, so the published npm package
  always matches the addon release. The `cli/package.json` `version` committed in the repo is
  therefore advisory — the release pipeline overwrites it to match `plugin.cfg` before `npm publish`.

To bump the release version, edit `plugin.cfg`'s `version` only. Keeping `cli/package.json` in sync
manually is optional (the pipeline forces it), but recommended so local `cli/` runs report the right
number.

---

## How to cut a release

The release is fully automated by [`.github/workflows/release.yml`](../.github/workflows/release.yml)
(which calls [`deploy.yml`](../.github/workflows/deploy.yml) for the npm publish). The pipeline runs
on **every push to `main`** but is **version-tag-gated**: it cuts a release only when
`plugin.cfg`'s version does not yet have a matching `v<version>` tag. A plain merge that does not
bump the version is therefore a **no-op** — nothing is released and nothing is published.

1. **Bump the version.** Edit the `version` field in
   [`addons/godot_mcp/plugin.cfg`](../addons/godot_mcp/plugin.cfg) to the new semver
   (e.g. `version="0.2.0"`). This is the single canonical version source (see
   [Version source of truth](#version-source-of-truth-addon--cli-reconciliation)). Land it on `main`
   through the normal PR flow. No manual tag push is required — the pipeline derives the tag from
   `plugin.cfg`.

2. **The pipeline runs on the merge to `main`.** `release.yml`:
   - `check-version-tag` reads `version` from `plugin.cfg`, computes `v<version>`, and checks whether
     that tag already exists. If it does (no bump), **every** downstream job is skipped — the run is a
     no-op. If it does not (a real bump), the gate opens;
   - the test gate runs as `needs:` — the .NET build+test, the `godot-mcp-cli` node tests
     (`test_cli.yml`, Node 20/22), and the Godot engine smoke matrix (`test_godot_plugin.yml` for
     4.3/4.4/4.5). The release proceeds only if all pass;
   - `release-addon` zips `addons/godot_mcp/` into `godot-mcp-addon-<version>.zip` (excluding
     `*.uid`, `*.import`, `bin/`, `obj/`, `.godot/`) and creates the **GitHub Release** + `v<version>`
     tag with the zip attached and auto-generated notes (`generate_release_notes: true`);
   - `deploy` (deploy.yml) sets `cli/package.json` to the release version, runs `npm ci && npm run
     build && npm test`, then **publishes `godot-mcp-cli` to npm** with `npm publish --access public
     --provenance` via **OIDC Trusted Publishing** (no `NPM_TOKEN` — see
     [npm Trusted Publisher prerequisite](#npm-trusted-publisher-prerequisite-first-publish-only)).

3. **Verify.** Check the new Release at
   `https://github.com/IvanMurzak/Godot-MCP/releases/tag/v0.2.0` (zip attached, notes right) and the
   npm package at `https://www.npmjs.com/package/godot-mcp-cli`.

### npm Trusted Publisher prerequisite (first publish only)

The npm publish uses **OIDC Trusted Publishing** — there is intentionally **no `NPM_TOKEN` secret**.
Before the **first** real publish can succeed, the maintainer must configure a **Trusted Publisher**
for the `godot-mcp-cli` package on npmjs.com, authorizing this repository and the `deploy.yml`
workflow. See <https://docs.npmjs.com/trusted-publishers>. Until that is configured, the `npm
publish` step fails authentication; the addon GitHub Release still succeeds (it does not depend on
npm). This is a deliberate maintainer gate (see [GATES](#gates--what-requires-the-maintainer-ivan)).

### Manual re-run

`release.yml` also accepts a `workflow_dispatch`. Dispatch is useful if `main` already carries a
bumped-but-unreleased version (e.g. a previous run failed after the bump landed). The same
version-gate applies: if the tag already exists, the dispatch run is a no-op.

---

## Godot Asset Library submission (metadata — manual, Ivan-gated)

The Godot Asset Library is the discoverable in-editor channel (*AssetLib* tab). Submission is a
**manual web-form step** performed by the maintainer at
<https://godotengine.org/asset-library/> after a GitHub Release exists. It is **not** automated and
**not** performed by this pipeline. The metadata below is prepared so the form can be filled in
quickly; nothing here submits anything.

| Field | Value |
| --- | --- |
| **Asset name** | Godot-MCP |
| **Category** | Tools |
| **Godot version** | 4.3+ (C#/.NET mono edition) |
| **Repository host** | GitHub |
| **Repository URL** | `https://github.com/IvanMurzak/Godot-MCP` |
| **Version** | the semver just released (e.g. `0.2.0`) — match `plugin.cfg` / the `v*` tag |
| **Version string / commit** | the released tag's commit (the Asset Library entry points at a specific commit or the release download) |
| **Download / commit** | the `v<semver>` tag commit on `main` (Asset Library can reference the tagged commit; the GitHub Release zip is the human download) |
| **License** | Apache-2.0 (matches [`LICENSE`](../LICENSE) and the `plugin.cfg` author) |
| **Icon** | a 128×128 PNG/SVG icon. **NONE ships in `addons/godot_mcp/` today** — add one (e.g. `addons/godot_mcp/icon.png`) before the first Asset Library submission and reference its raw GitHub URL in the form. |
| **Short description** | "Model Context Protocol (MCP) integration for the Godot Editor. AI tools in C#, cloud-connected to ai-game.dev." (matches `plugin.cfg` `description`) |
| **Long description** | The Godot counterpart of Unity-MCP: a C# editor addon that exposes Godot Editor operations (nodes, scenes, resources, scripts, screenshots, editor state, reflection) as **AI Tools** over an MCP server. See [`README.md`](../README.md) for the full tool family list and install steps. **Important install note for consumers:** because Godot compiles every `.cs` under the project into one assembly, the consumer's `.csproj` must declare the two NuGet `PackageReference`s the addon needs (`com.IvanMurzak.ReflectorNet` 5.3.1, `com.IvanMurzak.McpPlugin` 6.5.5) — surface this in the submission so installers aren't surprised by a compile error. |

After the form is submitted, a Godot Asset Library moderator reviews and approves the entry; later
versions are pushed as edits to the same entry (bump the version + point at the new tag).

---

## NuGet decision

**Godot-MCP does NOT publish its own NuGet package.** It *consumes* two upstream packages from
nuget.org as `PackageReference`s, pinned and owned by the upstream release pipelines (never bumped
in this repo):

| Package | Version | Role |
| --- | --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.3.1` | reflection / serialization core |
| `com.IvanMurzak.McpPlugin` | `6.5.5` | MCP plugin client (transitively pulls `McpPlugin.Common` + `ReflectorNet`) |

The addon **source folder** — distributed via the GitHub Release zip and the Godot Asset Library
entry — plus the **`godot-mcp-cli` npm package** are the distribution channels for Godot-MCP. There
is no new NuGet package ID to claim and no NuGet publish secret to configure; `release.yml` /
`deploy.yml` contain **no** `dotnet pack` / `dotnet nuget push` step, by design. (This is the
deliberate difference from the sibling `ReflectorNet` / `MCP-Plugin-dotnet` repos, whose `deploy.yml`
workflows DO push NuGet packages — those repos ARE the upstream publishers; Godot-MCP only *consumes*
NuGet packages, and publishes its **cli to npm** rather than to NuGet.)

---

## GATES — what requires the maintainer (Ivan)

Every action below is intentionally **out of scope for automation / agents** and must be performed
by the maintainer:

- **The version bump that triggers a release.** Releasing is gated on `plugin.cfg`'s version: only a
  maintainer-approved version bump landing on `main` opens the release gate. A no-bump merge is a
  no-op. No agent bumps the version to force a release.
- **Configuring the npm Trusted Publisher** for `godot-mcp-cli` (one-time, before the first publish).
  A manual step on npmjs.com authorizing this repo + `deploy.yml` to publish via OIDC. Until it is
  done, the `npm publish` step fails auth (the GitHub Release still succeeds). See
  [npm Trusted Publisher prerequisite](#npm-trusted-publisher-prerequisite-first-publish-only).
- **Godot Asset Library submission (and version edits).** A manual web-form step at
  <https://godotengine.org/asset-library/>, never automated.
- **Adding the addon icon** required by the Asset Library form (none ships today).
- **Any future NuGet publishing** — new package IDs, `dotnet nuget push` steps, or NuGet
  publish secrets / trusted-publishing config. None exist today and none should be added without an
  explicit maintainer decision (the NuGet model is consume-only; see
  [NuGet decision](#nuget-decision)).
