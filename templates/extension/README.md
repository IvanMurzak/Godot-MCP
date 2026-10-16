# Godot extension scaffold

From the Godot-MCP repository root, run:

```sh
python scripts/new_extension.py MyFeature
```

This creates `extensions/Godot-AI-MyFeature/` and registers the module and both catalog
mirrors. Replace the sample tools and add matching unit tests and live manifest assertions.
Use the shared test suite and independent release workflow described in
[the monorepo guide](../../docs/EXTENSION-MONOREPO.md).

The package remains source-only: consumer projects supply their own Godot SDK.
Keep `IncludeBuildOutput=false`, the package-named MSBuild props, and private GodotSharp
authoring references. Read [the packaging recipe](docs/source-only-nuget-recipe.md).
Legacy standalone initialization scripts and workflow samples are retained as historical
scaffold material; the monorepo generator excludes them from active module workflows.
