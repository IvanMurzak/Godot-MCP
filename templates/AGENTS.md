# templates/

- `extension/` — source-only NuGet extension scaffold, including package/tests/fixtures.

Templates do not compile into core or test consumers. New modules belong in
`../extensions/Godot-AI-<Feature>/` and must be registered in the explicit inventory.
