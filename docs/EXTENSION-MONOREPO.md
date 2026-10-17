# Independent extension modules

```
Godot-MCP/
├── addons/godot_mcp/                 core addon
├── cli/                             godot-cli (released with core)
├── extensions/
│   ├── manifest.json                explicit module inventory
│   └── Godot-AI-<Feature>/
│       ├── extension.json           identity, version, SDK floor, test fixtures
│       ├── src/Godot-AI-<Feature>/   independent source-only NuGet package
│       ├── tests/                   original xUnit and live tool assertions
│       └── testbed/                 standalone consumer example
├── templates/extension/             new module scaffold
└── .artifacts/extensions/<SDK>/<run>/
    ├── core/                        compile only, zero extensions
    ├── Godot-AI-<Feature>/           compile only, exactly one extension
    └── combined/                    ONE Godot editor for compatible extensions
```

Run `python scripts/test_extensions.py --sdk 4.5.1 --godot <Godot-mono-executable> --prove-isolation`.
The command builds each authoring project in Debug (including `#if TOOLS`) against its declared
dependency floors, runs all original unit tests, packs actual source-only packages into a private feed,
compiles independent consumers, then runs every compatible module's live manifest through one
editor and one local MCP server. It downloads the core's pinned server and test-only third-party
addons. Use `--server` to supply that server locally. Owned processes are stopped on success or failure.
`--unit-only` and `--prepare-only` explicitly omit the live editor stage.

Each SDK/profile gets new project and cache directories; editors never migrate a shared project.
The CI matrix is 4.3.0, 4.4.0, 4.5.1, 4.6.3 and 4.7.0. Modules requiring 4.4 are excluded only
from 4.3. Existing core runtime, installation and reload tests remain separate lifecycle tests.
The extension suite uses one editor **per supported version**, not one editor across all versions.

Isolation is a required compile gate. The core consumer sees no module sources; a single-module
consumer sees only its NuGet sources. A deliberate reference to Beehave passes in the combined
project and must fail in the Animation-only project. This catches the undeclared dependency
that a combined editor alone could conceal. Live assertions, success envelopes and response
expectations are retained. Script and assembly errors, and any other engine error, also fail the suite.
Godot 4.4's [upstream cold-import progress-dialog diagnostic](https://github.com/godotengine/godot/issues/103398)
is also matched by exact native frames, only in the log captured before the first tool call,
and reported separately. It remains fatal during tool calls or on other engine versions.
Dialogic fixtures use alpha-18 on 4.4 (alpha-19 editor resources require `DPITexture`, introduced
in 4.5) and alpha-20 on 4.5+, since alpha-20 contains the
upstream Godot 4.7 parser fix and requires Godot 4.5. These are test fixtures, not bundled addons.

## Releases and installation

Versions remain independent. Bump one module with
`python scripts/extension_release.py bump --only Godot-AI-Animation --version 0.1.3`.
Dispatch `release_extensions.yml` on main with `extensions=Godot-AI-Animation` (or a comma-separated
selection / `all`). Dry run defaults to true. All test gates precede publication.
NuGet Trusted Publishing must authorize `IvanMurzak/Godot-MCP`, `release_extensions.yml`, and
the package family `com.IvanMurzak.Godot.MCP.*`; repository variable `NUGET_USER` is the owning
NuGet account name. No permanent publishing key is required.

```
NuGet.org
└── com.IvanMurzak.Godot.MCP.Animation / <its-version> / source-only .nupkg
GitHub Godot-MCP Releases
├── v<core-version>                  addon ZIP + core/CLI release
├── animation-v<its-version>         Animation.nupkg + SHA256SUMS
└── particles-v<its-version>         Particles.nupkg + SHA256SUMS
```

Unchanged versions reuse the immutable registry package and are not rebuilt or republished.
All missing packages are submitted before waiting for NuGet validation/indexing (up to one hour).
The workflow then verifies their public payloads before creating GitHub component releases.
Existing published assets must match; retries resume drafts without replacing public assets.
Component releases never replace the core's GitHub `latest` release.

## Godot Asset Library

Each component also carries `godot-mcp-<feature>-v<version>-assetlib.zip`, its `.sha256`,
and `assetlib-submission.json` on the same independent GitHub release. The ZIP contains
only that extension's C# sources, README, license and installation instructions under
`addons/godot_mcp_extensions/<Feature>/`. It includes no core, sibling modules, DLLs,
test projects or third-party addons. Godot MCP core must already be installed with its
NuGet dependencies configured. Use the source ZIP or the extension's NuGet package in
a project; installing both produces duplicate C# types.

`release_extensions.yml` submits initial entries and later version edits through the
Asset Library API using the existing `GODOT_ASSETLIB_USERNAME` and `GODOT_ASSETLIB_PASSWORD`
repository secrets. `Custom` download URLs point at the selected component ZIP, not the
monorepo source archive. The core's existing `assetlib-edit` release job is unchanged.

Submissions require moderator approval. A receipt with `status: submitted` records a
pending edit ID, not a public catalog entry. The publisher reconciles existing owned
assets and pending edits before submitting, reuses matching submissions on retry, and
does not duplicate or overwrite an in-review edit. Unchanged ZIPs and receipts are reused.
The Train verifies their version, archive URL/digest and submission ID when the inventory's
`assetLibrary` flag is enabled. Approval by Godot moderators remains external to the release.

Every supported SDK compiles an additional consumer containing the extracted source ZIPs
without extension NuGet references. The existing isolated NuGet consumers, sibling-reference
canary and combined one-editor suite remain in place; the archive check launches no editor.

The CLI and dock resolve each package's latest stable version from NuGet, independently of
GitHub tags. Package IDs stay unchanged, so historical versions remain installable. Extensions
contain C# sources and MSBuild props, not compiled Godot DLLs; they compile against the user's SDK.
Third-party addon fixtures are never included in extension packages.

## Add a module

Run `python scripts/new_extension.py MyFeature` from this repository. It copies the scaffold
into `extensions/Godot-AI-MyFeature/`, registers metadata and both shared catalog mirrors (with an
empty `tools` list), and does not create a repository or separate workflows. An extension is an
**RQL module** ([docs/rql.md](rql.md#extensions-are-rql-modules)): implement its adapters, sources and
guide under `Editor/Rql/`, the xUnit tests, and the live manifest that runs every guide example, then
run the same central suite. Packaging constraints are documented in each module's
`docs/source-only-nuget-recipe.md`.

The live manifests call the core `rql-query` / `rql-mutate` / `rql-schema` tools and match the default
compact text results; `scripts/extension_e2e.py` drives them. A call marked `expectFailure` must fail with
its `expect` substrings (a value the module rejects); its error response is tolerated only in the editor-log
lines written during that call. Every test project links the shared guide checks in `extensions/Shared/Tests/`.
