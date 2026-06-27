# Shared extension catalog — `extensions.catalog.json`

`extensions.catalog.json` (this folder) is the **single source of truth** for the Godot-MCP
**extension catalog** — the list of optional AI-tool-family packages a consumer project can install.
It is consumed by all THREE install channels so they can never drift:

| Channel | How it consumes the catalog |
| --- | --- |
| **Dock** (in-editor `Extensions` panel) | The pure-managed `GodotExtensionRegistry` parses this JSON, which is **embedded into the addon assembly** as `Godot-MCP.extensions.catalog.json` (`<EmbeddedResource>` in `Godot-MCP.csproj`). `GodotExtensionCatalog.LoadEmbedded()` reads it; the dock binds to `GodotExtensionRegistry.All`. |
| **CLI** (`godot-cli install-extension`) | The CLI ships a typed mirror, `cli/src/utils/extensions-catalog.ts`, kept **byte-identical** to this JSON by the parity test `cli/tests/extensions-catalog-parity.test.ts` (the same single-source discipline `addon-deps.ts` / `addon-deps-parity.test.ts` use for the NuGet pins). The mirror avoids a runtime `../addons` dependency so the published npm package stays self-contained. |
| **App** (`AI-Game-Dev-App`, later) | Imports the catalog + `installExtension` from the `godot-cli` library (`import { EXTENSIONS_CATALOG, installExtension } from 'godot-cli'`). It never re-implements the list, so it inherits the CLI's parity guarantee transitively. |

## Schema

```jsonc
{
  "schemaVersion": 1,
  "extensions": [
    {
      "name": "ProBuilder Tools",                       // display name (row title)
      "description": "Mesh-editing MCP tools for Godot.", // one-line description
      "packageId": "com.IvanMurzak.Godot.MCP.ProBuilder", // INSTALL IDENTITY — the NuGet <PackageReference Include="...">
      "version": "1.2.0",                                  // OPTIONAL pin; omit for a floating reference
      "gitUrl": "https://github.com/IvanMurzak/...",       // OPTIONAL repo/docs link
      "tools": [                                           // OPTIONAL contributed-tool list
        { "name": "probuilder-extrude", "description": "Extrude faces." }
      ]
    }
  ]
}
```

- `packageId` is the **install identity**. Installing an extension adds a
  `<PackageReference Include="<packageId>" Version="<version>" />` to the consumer project's
  `.csproj` (Godot compiles every `.cs` under the project into one assembly, so the package's
  `[AiToolType]` tool families compile into the consumer's project). Installed state is detected by
  matching this id against the consumer's existing package references.
- `version` is OPTIONAL. When present it pins the reference AND drives the up-to-date / update-available
  decision (numeric, component-wise compare — `1.10.0` > `1.2.0`). When absent the reference is added
  WITHOUT a `Version` attribute (floating / centrally-managed), and a present reference is never bumped.
- `name`, `packageId` are REQUIRED and non-empty; entries missing either are ignored by both parsers.

## Ships empty (for now)

No Godot-MCP extension package exists on nuget.org yet, so `extensions` is `[]` and the dock renders an
honest "coming soon" placeholder. To add the first real extension once it is published: append ONE entry
to `extensions` here — no other file changes (the dock reads it via the embedded resource; the CLI's
parity test forces `extensions-catalog.ts` to be updated to match, which the CLI then consumes).

## Behavioral parity contract

The CLI's `install-extension` (`cli/src/utils/extension-install.ts`) MUST stay behaviorally identical to
the dock's `ExtensionInstaller` / `ExtensionInstallPlanner` (`addons/godot_mcp/Runtime/Extensions/`):
add-when-absent, bump-only-upward, no-op-when-equal-or-newer, no-op-when-descriptor-unversioned, and the
numeric-tolerant version compare. The shared scenario set is encoded on BOTH sides
(`Godot-MCP.Tests/ExtensionInstallTests.cs` and `cli/tests/extension-install.test.ts`) so a divergence in
either implementation fails a test.
