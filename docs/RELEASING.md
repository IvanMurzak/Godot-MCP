# Releasing Godot-MCP

How a new version of **Godot-MCP** is built, published, and distributed.

There are two distribution artifacts, both cut from the **same release version**:

1. the **addon source folder** (`addons/godot_mcp/`), shipped as
   - a **GitHub Release** with a versioned `godot-mcp-addon-<version>.zip` attached, and
   - a **Godot Asset Library** entry that points at that repository/version; and
2. the **`godot-cli`** npm package (`cli/`), published to the public npm registry.

Godot-MCP does **not** publish any NuGet package of its own — see [NuGet](#nuget-decision) below.

The **MCP server is NOT released from this repo.** The addon consumes the shared
[GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) (binary `gamedev-mcp-server`),
released from its own repo on its own version line. See
[Server version pin](#server-version-pin-shared-gamedev-mcp-server) below.

## Server version pin (shared GameDev-MCP-Server)

The addon downloads and runs the shared server release pinned by the **`ServerVersion` constant** in
[`addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`](../addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs).
The addon version (`plugin.cfg`, 0.x) and the server version (8.x) are **decoupled** — never derive one
from the other. Bumping the consumed server = changing the `ServerVersion` constant. **Ordering rule:** the
pinned `v<ServerVersion>` release (with all 7 `gamedev-mcp-server-<rid>.zip` assets) must already exist on
GameDev-MCP-Server **before** cutting an addon release that pins it — otherwise every consumer's local
server download 404s.

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

1. **Bump the version.** Run the release helper from the repository root:

   ```bash
   node scripts/bump-version.mjs 0.2.0 --dry-run
   node scripts/bump-version.mjs 0.2.0
   ```

   The helper updates the canonical `addons/godot_mcp/plugin.cfg` version plus the release-adjacent
   files that must stay in sync (`cli/package*.json`, the fallback plugin version, and the Asset
   Library submission notes). Land the resulting changes on `main` through the normal PR flow. No
   manual tag push is required — the pipeline derives the tag from `plugin.cfg`.

2. **The pipeline runs on the merge to `main`.** `release.yml`:
   - `check-version-tag` reads `version` from `plugin.cfg`, computes `v<version>`, and checks whether
     that tag already exists. If it does (no bump), **every** downstream job is skipped — the run is a
     no-op. If it does not (a real bump), the gate opens;
   - the test gate runs as `needs:` — the .NET build+test, the `godot-cli` node tests
     (`test_cli.yml`, Node 20/22), and the Godot engine smoke matrix (`test_godot_plugin.yml` for
     4.3/4.4/4.5). The release proceeds only if all pass;
   - `release-addon` zips `addons/godot_mcp/` into `godot-mcp-addon-<version>.zip` (excluding
     `*.uid`, `*.import`, `bin/`, `obj/`, `.godot/`) and creates the **GitHub Release** + `v<version>`
     tag with the zip attached and auto-generated notes (`generate_release_notes: true`);
   - `deploy` (deploy.yml) sets `cli/package.json` to the release version, runs `npm ci && npm run
     build && npm test`, then **publishes `godot-cli` to npm** with `npm publish --access public
     --provenance` via **OIDC Trusted Publishing** (no `NPM_TOKEN` — see
     [npm Trusted Publisher prerequisite](#npm-trusted-publisher-prerequisite-first-publish-only)).

3. **Verify.** Check the new Release at
   `https://github.com/IvanMurzak/Godot-MCP/releases/tag/v0.2.0` (zip attached, notes right) and the
   npm package at `https://www.npmjs.com/package/godot-cli`.

### npm Trusted Publisher prerequisite (first publish only)

The npm publish uses **OIDC Trusted Publishing** — there is intentionally **no `NPM_TOKEN` secret**.
Before the **first** real publish can succeed, the maintainer must configure a **Trusted Publisher**
for the `godot-cli` package on npmjs.com, authorizing this repository and the `deploy.yml`
workflow. See <https://docs.npmjs.com/trusted-publishers>. Until that is configured, the `npm
publish` step fails authentication; the addon GitHub Release still succeeds (it does not depend on
npm). This is a deliberate maintainer gate (see [GATES](#gates--what-requires-the-maintainer-ivan)).

### Manual re-run

`release.yml` also accepts a `workflow_dispatch`. Dispatch is useful if `main` already carries a
bumped-but-unreleased version (e.g. a previous run failed after the bump landed). The same
version-gate applies: if the tag already exists, the dispatch run is a no-op.

---

## Godot Asset Library submission

The Godot Asset Library is the discoverable in-editor channel (*AssetLib* tab). It has two distinct
operations:

- The **one-time INITIAL submission** of a brand-new asset entry, and the **moderator approval** that
  follows it — these remain **manual web-form steps** performed by the maintainer at
  <https://godotengine.org/asset-library/>. They are NOT automated (see
  [First submission](#first-submission-one-time) and [GATES](#gates--what-requires-the-maintainer-ivan)).
- The **per-release VERSION EDIT** (bumping **Version** + **Download Commit** on the *existing* entry on
  every later release) is now **automated** by the `assetlib-edit` job in
  [`release.yml`](../.github/workflows/release.yml). On each real version bump that cuts a release, after
  the GitHub Release is created, the job submits the edit via the pinned
  [`deep-entertainment/godot-asset-lib-action`](https://github.com/deep-entertainment/godot-asset-lib-action)
  (`addEdit` mode). The Godot moderator still reviews each submitted edit before it goes live.

The version edit no longer requires opening the web form by hand for routine releases — but the
**INITIAL** submission (which creates the asset id the automation targets) is still a manual first step.

A **ready-to-paste submission package** — every form field's exact value, plus the icon and preview
URLs — is maintained at [`docs/assetlib/SUBMISSION.md`](assetlib/SUBMISSION.md). Use it for the manual
INITIAL submission; the automated per-release edit renders the same field values from
[`.github/assetlib/edit.hbs`](../.github/assetlib/edit.hbs) (keep the two in sync).

### Automated version edit — required GitHub config

The `assetlib-edit` job reads its credentials and the asset id ONLY from GitHub-managed config (nothing
is hardcoded). Set these in the repository **Settings → Secrets and variables → Actions**:

| Name | Kind | Purpose |
| --- | --- | --- |
| `GODOT_ASSETLIB_USERNAME` | **Secret** | The maintainer's godotengine.org Asset Library username. |
| `GODOT_ASSETLIB_PASSWORD` | **Secret** | That account's Asset Library password (the action logs in and obtains a session token). |
| `GODOT_ASSETLIB_ASSET_ID` | **Variable** | The numeric asset id of the existing Godot-MCP entry (assigned by the Asset Library after the INITIAL submission is approved). |

Until all three are configured, the `assetlib-edit` job fails authentication and shows up as a **red
job** — but it is isolated: the GitHub Release and the npm `deploy` publish are unaffected (they do not
depend on it). The asset id only exists after the INITIAL submission, so this automation becomes
effective from the **second** release onward.

The rendered field values (title, description, category, godot_version, license, repo/issues URLs, icon
URL) live in [`.github/assetlib/edit.hbs`](../.github/assetlib/edit.hbs); `version_string` and
`download_commit` are injected at run time from the canonical version (`plugin.cfg` via
`check-version-tag`) and the released commit (`github.sha`).

The notes below are the manual-procedure reference; the field values live in `SUBMISSION.md` /
`edit.hbs` so they are versioned and easy to update.

### Form-field requirements (verified against the Asset Library docs)

The submission form (per the
[official docs](https://docs.godotengine.org/en/stable/community/asset_library/submitting_to_assetlib.html))
has these field constraints worth knowing before you start:

- **Category** decides whether the entry is an **Addon** or a **Project** — there is no separate "Type"
  field. The category list is split into Addon-side and Project-side groups; pick an Addon-side category
  so the entry shows in the in-editor *AssetLib* tab (a Project entry is visible only in the Project
  Manager). Use **Tools** — the Addon-side categories are: 2D Tools, 3D Tools, Shaders, Materials,
  **Tools**, Scripts, Misc.
- **Icon URL** must be a **square (1:1) PNG or JPG, minimum 128×128** — **SVG is NOT accepted**. A raw
  GitHub URL is required, e.g.
  `https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/main/addons/godot_mcp/icon.png`. A
  512×512 PNG ships at [`addons/godot_mcp/icon.png`](../addons/godot_mcp/icon.png) for exactly this.
- **Godot version** is a **single version per submission** — submit against the lowest supported
  (`4.3`); the addon also runs on 4.4 / 4.5 but each extra version would need its own entry.
- **Download Commit** is a specific commit **hash** (not a tag): use the commit that the `v<version>`
  release tag points at. The Asset Library downloads a GitHub *source archive* of the repo at that
  commit. The root [`.gitattributes`](../.gitattributes) `export-ignore` entries (see
  [Why a `.gitattributes` export-ignore](#why-a-gitattributes-export-ignore) below) trim that archive
  down to just `addons/godot_mcp/` (plus `LICENSE` / `README.md`), so the consumer receives the addon
  source under `addons/godot_mcp/` exactly as it sits on `main` at that tag — and nothing else.
- **License** = **Apache-2.0**.
- **Description** is **plain text** today (Markdown is planned but not live) — keep it prose, no
  Markdown syntax.
- **Preview** (optional) accepts up to three images / YouTube videos with thumbnail URLs; use the
  promo images under `docs/img/promo/` via their raw URLs.

### Why a `.gitattributes` export-ignore

The Asset Library does **not** download the curated `godot-mcp-addon-<version>.zip` from the GitHub
Release — it downloads a **GitHub source archive of the whole repo** at the Download Commit. Without
intervention that archive would carry `cli/`, `Godot-MCP.Tests/`, `Godot-Tests/`,
`Godot-MCP.csproj`, `Godot-MCP.sln`, `docs/`, `.github/`, and `CLAUDE.md` straight into the consumer's
project. Because Godot's mono build compiles **every** `.cs` under a project into one assembly, that
stray test C# would land in the consumer's project and **break their build** — the opposite of
the install story this release promises.

The root [`.gitattributes`](../.gitattributes) prevents this. Its
[`export-ignore`](https://git-scm.com/docs/gitattributes#_creating_an_archive) entries mark every
top-level path **except** `addons/` (and `LICENSE` / `README.md`) so `git archive` — and therefore the
GitHub source archives the Asset Library serves — omit them. The result is an addon-only snapshot.

This is **archive-only**: `export-ignore` changes nothing in the working tree, in CI, or in the
`release.yml` `release-addon` job (which zips `addons/godot_mcp/` directly and never calls
`git archive`). After editing the ignore list, verify the snapshot contains only the intended paths:

```bash
git archive HEAD | tar -t          # should list only addons/, LICENSE, README.md
```

The `.gitattributes` lives at the tagged commit, and that tagged snapshot is immutable once the Asset
Library references it — so the export-ignore set must be correct **in the release PR**, not patched
afterwards.

### First submission (one-time)

1. Cut the GitHub Release first (the version bump → `release.yml` → `v<version>` tag + addon zip). The
   Asset Library entry references the released commit, so the release must exist.
2. Sign in at <https://godotengine.org/asset-library/> with the maintainer's godotengine.org account.
3. Click **Submit Asset** and fill every field from
   [`docs/assetlib/SUBMISSION.md`](assetlib/SUBMISSION.md). Set **Download Commit** to the commit hash
   the `v<version>` tag points at (`git rev-list -n1 v<version>`).
4. Submit. A Godot Asset Library moderator reviews and approves the entry (this can take days).

### Subsequent-version updates (every later release — AUTOMATED)

The Asset Library entry is **edited in place** — you do NOT create a new entry per version. Once the
INITIAL submission exists and the three GitHub config entries above are set, this is **automated**:

1. Cut the new GitHub Release (bump `plugin.cfg`, merge → `release.yml` tags `v<newversion>`).
2. The `assetlib-edit` job runs after the Release and submits the edit automatically — it bumps the
   **Version** to the new `plugin.cfg` semver (`version_string`) and sets **Download Commit** to the
   released commit (`github.sha`). The other field values come from `.github/assetlib/edit.hbs`.
3. The Godot Asset Library moderator reviews the submitted edit before it goes live (same as a manual
   edit — automation only fills + submits the form, it does not bypass moderation).

If the automated edit fails (red `assetlib-edit` job — e.g. credentials not yet configured, or the
asset id is wrong), fall back to the manual edit: sign in, open the existing **Godot-MCP** asset, click
**Edit**, bump **Version** + **Download Commit** (`git rev-list -n1 v<newversion>`), and submit.

> Keep [`.github/assetlib/edit.hbs`](../.github/assetlib/edit.hbs) and
> [`docs/assetlib/SUBMISSION.md`](assetlib/SUBMISSION.md) in sync, and update both (plus the long
> description / previews) only when a field actually changes — so the automated edit and the manual
> reference always reflect what is live.

---

## NuGet decision

**Godot-MCP does NOT publish its own NuGet package.** It *consumes* two upstream packages from
nuget.org as `PackageReference`s, pinned and owned by the upstream release pipelines (never bumped
in this repo):

| Package | Version | Role |
| --- | --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.3.2` | reflection / serialization core |
| `com.IvanMurzak.McpPlugin` | `7.2.0` | MCP plugin client (transitively pulls `McpPlugin.Common` + `ReflectorNet`) |

The addon **source folder** — distributed via the GitHub Release zip and the Godot Asset Library
entry — plus the **`godot-cli` npm package** are the distribution channels for Godot-MCP. There
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
- **Configuring the npm Trusted Publisher** for `godot-cli` (one-time, before the first publish).
  A manual step on npmjs.com authorizing this repo + `deploy.yml` to publish via OIDC. Until it is
  done, the `npm publish` step fails auth (the GitHub Release still succeeds). See
  [npm Trusted Publisher prerequisite](#npm-trusted-publisher-prerequisite-first-publish-only).
- **Godot Asset Library INITIAL submission + moderator approval.** Creating the brand-new asset entry
  is a manual web-form step at <https://godotengine.org/asset-library/>, and every submission (initial
  or edit) is reviewed by a Godot moderator. These are NOT automated. (The per-release **version edit**
  on the existing entry IS automated — see
  [Automated version edit](#automated-version-edit--required-github-config) — but it still goes through
  moderator review, and it depends on the maintainer having configured the
  `GODOT_ASSETLIB_USERNAME` / `GODOT_ASSETLIB_PASSWORD` secrets and the `GODOT_ASSETLIB_ASSET_ID`
  variable.)
- **Adding the addon icon** required by the Asset Library form (none ships today).
- **Any future NuGet publishing** — new package IDs, `dotnet nuget push` steps, or NuGet
  publish secrets / trusted-publishing config. None exist today and none should be added without an
  explicit maintainer decision (the NuGet model is consume-only; see
  [NuGet decision](#nuget-decision)).
