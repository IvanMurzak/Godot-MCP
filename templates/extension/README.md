# Godot extension scaffold

From the Godot-MCP repository root, run:

```sh
python scripts/new_extension.py MyFeature
```

This creates `extensions/Godot-AI-MyFeature/` and registers the module and both catalog
mirrors. The scaffold is an **RQL module** (`Editor/Rql/`): an `IRqlModule` that the core addon
discovers in the consumer assembly, a sample `myfeature` source, and the agent guide that the core
renders into its `godot-rql` skill. Agents use the extension through the core `rql-query` /
`rql-mutate` / `rql-schema` tools; an extension adds no MCP tools. Replace the sample with adapters
for the Godot types the feature covers (validated members, methods), sources and runnable guide
examples, and keep `tests/e2e/e2e-tools.json` running every guide example (the xUnit suite checks it).
Use the shared test suite and independent release workflow described in
[the monorepo guide](../../docs/EXTENSION-MONOREPO.md) and the RQL design in [docs/rql.md](../../docs/rql.md).

The package remains source-only: consumer projects supply their own Godot SDK.
Keep `IncludeBuildOutput=false`, the package-named MSBuild props, and private GodotSharp
authoring references. Read [the packaging recipe](docs/source-only-nuget-recipe.md).
Legacy standalone initialization scripts and workflow samples are retained as historical
scaffold material; the monorepo generator excludes them from active module workflows.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - YOUR_FEATURE** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
