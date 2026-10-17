/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.Linq;
using System.Text;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The <c>godot-rql</c> skill: the language, the editor operations, and — generated from the live engine — every source
    /// and function plus the guides that modules contribute. An extension that adds an RQL module (sources, adapters,
    /// <c>ctx.AddGuide</c>) therefore appears in this skill as soon as it is installed. The Godot counterpart of Unity-MCP's
    /// <c>unity-rql</c> skill.
    /// </summary>
    [AiSkillType]
    public static class Skill_Rql
    {
        public const string SkillId = "godot-rql";

        [AiSkill(SkillId,
@"How to read and change anything in the Godot editor with RQL (rql-query / rql-mutate / rql-schema): the query
language, operators, sources (scene nodes, resources, files, scenes, classes, console logs, editor state), editor
operations (add / duplicate / reparent / remove nodes, scenes, resource files, selection, play) and what installed
extensions add.")]
        public static string Markdown => MainThread.Instance.Run(Render);

        public static string Render()
        {
            var engine = RqlGodot.Engine;
            var sb = new StringBuilder();
            // Plain strings, not verbatim ones: a line starting with '#' inside a verbatim string breaks the
            // ExportRelease build, where this file sits in a disabled #if TOOLS region the preprocessor still scans.
            sb.Append("# RQL — query and change the Godot editor\n\n")
              .Append("RQL is one pipeline language for the whole editor: find nodes and resources, read members and Godot properties, filter,\n")
              .Append("aggregate, and change them in one call. Prefer one precise query over many exploratory calls.\n\n")
              .Append("- `rql-query` — read-only queries, compact table results.\n")
              .Append("- `rql-mutate` — queries ending with `set` / `call`; all-or-nothing, undoable (Ctrl+Z), `dry_run: true` previews. Scene\n")
              .Append("  edits mark the scene unsaved (`editor | call SaveScene()` writes the `.tscn`); edited resource files are saved.\n")
              .Append("- `rql-schema` — sources and the node classes in the scene; `rql-schema(type)` lists one type's members, methods and\n")
              .Append("  Godot properties.\n\n")
              .Append("```bash\n")
              .Append("godot-cli run-tool rql-query --input '{\"query\": \"nodes | where name contains \\\"Enemy\\\" | select $ref name\"}'\n")
              .Append("godot-cli run-tool rql-mutate --input '{\"query\": \"nodes | where name == \\\"Player\\\" | set Visible = false\"}'\n")
              .Append("```\n\n")
              .Append("## Language\n\n```\n").Append(RqlGuide.Language).Append("\n```\n\n")
              .Append("## Examples\n\n```\n").Append(RqlGuide.QueryExampleText).Append("\n```\n\n")
              .Append("## Changing things (rql-mutate)\n\n```\n").Append(RqlGuide.MutateStages).Append(RqlGuide.MutateExamplesBlock).Append("```\n\n")
              .Append("## Sources\n\n| Source | What |\n|---|---|\n");
            foreach (var s in engine.Sources.Values.OrderBy(s => s.Name, System.StringComparer.Ordinal))
                sb.Append("| `").Append(s.Name).Append("` | ").Append(Cell(s.Description ?? "")).Append(" |\n");
            foreach (var f in engine.Functions.Keys.OrderBy(k => k, System.StringComparer.Ordinal))
                sb.Append("| `").Append(f).Append("(…)` | ").Append(Cell(engine.FunctionDescriptions.TryGetValue(f, out var d) ? d : "")).Append(" |\n");

            foreach (var g in engine.Guides)
                sb.Append("\n## ").Append(g.Key).Append("\n\n").Append(g.Value.Trim()).Append('\n');

            sb.Append("\n## Installed RQL modules\n\n");
            foreach (var m in engine.Modules)
                sb.Append("- `").Append(m.Module).Append('`').Append(m.Ok ? "" : " — failed: " + m.Error).Append('\n');
            return sb.ToString();
        }

        private static string Cell(string text) => text.Replace("|", "\\|").Replace("\n", " ");
    }
}
#endif
