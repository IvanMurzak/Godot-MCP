# CI and independent releases

Workflows live in the Godot-MCP repository. See the [monorepo guide](../../../docs/EXTENSION-MONOREPO.md) for the shared live editor, isolated compilation, supported SDK matrix and independent NuGet/GitHub releases.

Run from the repository root: `python scripts/test_extensions.py --sdk 4.5.1 --godot <Godot-mono-executable> --prove-isolation`.
Unit tests remain in this module; they do not require a Godot process.
