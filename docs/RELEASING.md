# Releasing Godot-MCP

How a new version of the **Godot-MCP** addon is built, published, and distributed.

The single distribution channel is the **addon source folder** (`addons/godot_mcp/`), shipped
two ways from the same artifact:

1. a **GitHub Release** with a versioned `godot-mcp-addon-<version>.zip` attached, and
2. a **Godot Asset Library** entry that points at that repository/version.

Godot-MCP does **not** publish any NuGet package of its own — see [NuGet](#nuget-decision) below.

---

## How to cut a release

The release is fully automated by [`.github/workflows/release.yml`](../.github/workflows/release.yml),
which fires **only on a `v*` tag push**. Merging to `main` does NOT cut a release.

1. **Bump the addon version.** Edit the `version` field in
   [`addons/godot_mcp/plugin.cfg`](../addons/godot_mcp/plugin.cfg) to the new semver
   (e.g. `version="0.2.0"`). This is the canonical version source for the addon. Commit it to `main`
   through the normal PR flow.
2. **Tag the release commit.** Once the version bump is on `main`, create and push a matching tag
   prefixed with `v`:

   ```bash
   git tag v0.2.0
   git push origin v0.2.0
   ```

   > The tag name (`v0.2.0`) is what names the GitHub Release; the workflow strips the leading `v`
   > for the zip filename (`godot-mcp-addon-0.2.0.zip`). Keep the tag's semver in lockstep with the
   > `plugin.cfg` `version` you bumped in step 1.

3. **The workflow runs.** On the tag push, `release.yml`:
   - restores + builds + tests `Godot-MCP.sln` (sanity gate — a tag on a red commit fails here
     before anything is published);
   - zips `addons/godot_mcp/` into `godot-mcp-addon-<version>.zip`, excluding dev cruft
     (`*.uid`, `*.import`, `bin/`, `obj/`, `.godot/`);
   - creates the **GitHub Release** named after the tag, with the zip attached and
     auto-generated release notes (`generate_release_notes: true`).

4. **Verify.** Check the new Release at
   `https://github.com/IvanMurzak/Godot-MCP/releases/tag/v0.2.0` and confirm the zip asset is
   attached and the notes look right.

### Dry run (no publish)

To exercise the build + zip without publishing a Release, trigger the workflow manually
(`workflow_dispatch`, e.g. via *Actions → release → Run workflow*). The manual path builds and zips
exactly as the tag path does, but uploads the zip as a **workflow artifact** only — the
Release-publish step is guarded by `if: startsWith(github.ref, 'refs/tags/')`, so a dispatch run
**never** creates a Release. Download the artifact from the run summary to inspect the package.

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
entry — is the **sole** distribution channel for Godot-MCP. There is no new package ID to claim and
no NuGet publish secret to configure; `release.yml` contains **no** `dotnet pack` / `dotnet nuget
push` step, by design. (This is the deliberate difference from the sibling `ReflectorNet` /
`MCP-Plugin-dotnet` repos, whose `deploy.yml` workflows DO push NuGet packages — those repos ARE the
upstream publishers; Godot-MCP is only a consumer.)

---

## GATES — what requires the maintainer (Ivan)

Every action below is intentionally **out of scope for automation / agents** and must be performed
by the maintainer:

- **The first `v*` tag / first Release.** No agent cuts a tag or runs `gh release create`. The
  maintainer pushes the `v<semver>` tag; `release.yml` then runs on its own.
- **Each subsequent release tag.** Same — bumping `plugin.cfg` lands via PR, but the tag that
  actually publishes is pushed by the maintainer.
- **Godot Asset Library submission (and version edits).** A manual web-form step at
  <https://godotengine.org/asset-library/>, never automated.
- **Adding the addon icon** required by the Asset Library form (none ships today).
- **Any future NuGet publishing** — new package IDs, `dotnet nuget push` steps, or NuGet
  publish secrets / trusted-publishing config. None exist today and none should be added without an
  explicit maintainer decision (the current model is consume-only; see
  [NuGet decision](#nuget-decision)).
