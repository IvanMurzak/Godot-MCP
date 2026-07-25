/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Skills
    {
        /// <summary>
        /// Long-form markdown injected into the generated <c>SKILL.md</c> body for
        /// <c>godot-skill-create</c> (via <c>[AiSkillBody]</c>) — the Godot counterpart of Unity-MCP's
        /// <c>Skills.Create.SkillBody.cs</c>. Split into its own partial-class file for the same reason
        /// Unity splits it: the sample + guidance dwarf the handler, and the 1024-char YAML
        /// <c>description:</c> cap means this content can only travel in the body.
        ///
        /// <para>
        /// Godot-specific throughout — Godot compiles EVERY <c>.cs</c> under the project into one assembly
        /// (so there is no Unity <c>.asmdef</c> placement problem, but the project's own
        /// <c>PackageReference</c>s must cover the addon's), the editor API must be reached on the main
        /// thread via ReflectorNet's dispatcher, and a new tool only becomes callable after the project is
        /// REBUILT (Godot builds C# out-of-band, unlike Unity's automatic domain reload).
        /// </para>
        /// </summary>
        internal const string SkillsCreateSkillBody =
            "Create a new skill (MCP tool) for the Godot Editor by writing a C# (`.cs`) file into the " +
            "project. Godot compiles every `.cs` under the project into ONE assembly, so after the project " +
            "is rebuilt the new tool is discovered by the addon's assembly scanner and becomes callable " +
            "through MCP.\n\n" +
            "## Inputs\n\n" +
            "- `path` — `res://` path of the C# file to write, e.g. `res://Skills/Tool_Sample.cs`. Must be a " +
            "`res://` FILE path ending in `.cs`, with no `..` segment. GDScript (`.gd`) is rejected: only C# " +
            "can carry the `[AiToolType]`/`[AiTool]` attributes the scanner discovers.\n" +
            "- `code` — the full C# source for the tool file.\n\n" +
            "## Behavior\n\n" +
            "Missing parent directories are created, an existing file at `path` is OVERWRITTEN (the " +
            "re-emit-after-a-fix loop), the file is reimported through the editor filesystem, and the tool " +
            "bounded-waits for the scan to settle before returning a structured `ScriptInfo`.\n\n" +
            "**Rebuild required.** Godot builds C# out-of-band (on editor focus, or an explicit *Build*), so " +
            "the new tool is NOT callable the instant this returns — unlike Unity, there is no automatic " +
            "domain reload. Trigger a build, then re-list the tools.\n\n" +
            "## Requirements for the file to compile\n\n" +
            "The CONSUMER project's `.csproj` must already declare the same NuGet `PackageReference`s the " +
            "addon depends on (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`) — the addon ships " +
            "as source, so its own csproj does not carry into the project. If they are missing, the whole " +
            "project assembly fails to compile, not just the new file.\n\n" +
            "## Full sample\n\n" +
            "```csharp\n" +
            "// This sample drives EditorInterface, so the WHOLE file is guarded: `TOOLS` is defined only\n" +
            "// in the editor build, and Godot compiles every .cs in the project into ONE assembly — so\n" +
            "// without this guard an exported game build fails to compile (see 'Guard editor-only code'\n" +
            "// below). A tool that touches no editor API needs no guard.\n" +
            "#if TOOLS\n" +
            "#nullable enable\n" +
            "using System;\n" +
            "using System.ComponentModel;\n" +
            "using com.IvanMurzak.McpPlugin;\n" +
            "using com.IvanMurzak.ReflectorNet.Utils;\n" +
            "using Godot;\n" +
            "\n" +
            "namespace com.IvanMurzak.Godot.MCP.Tools\n" +
            "{\n" +
            "    [AiToolType]\n" +
            "    public partial class Tool_Sample\n" +
            "    {\n" +
            "        public const string SampleRenameToolId = \"sample-rename\";\n" +
            "\n" +
            "        [AiTool(SampleRenameToolId, Title = \"Sample / Rename\")]\n" +
            "        [Description(\"Renames a node in the currently edited scene.\")]\n" +
            "        public string Rename\n" +
            "        (\n" +
            "            [Description(\"Node path of the node to rename, e.g. '/root/Main/Player'.\")]\n" +
            "            string nodePath,\n" +
            "            [Description(\"New name to assign.\")]\n" +
            "            string newName\n" +
            "        )\n" +
            "        {\n" +
            "            if (string.IsNullOrEmpty(newName))\n" +
            "                throw new ArgumentException(\"New name cannot be null or empty.\", nameof(newName));\n" +
            "\n" +
            "            return MainThread.Instance.Run(() =>\n" +
            "            {\n" +
            "                var root = EditorInterface.Singleton.GetEditedSceneRoot()\n" +
            "                    ?? throw new InvalidOperationException(\"No scene is currently open in the editor.\");\n" +
            "\n" +
            "                var node = root.GetNodeOrNull(nodePath)\n" +
            "                    ?? throw new ArgumentException($\"Node '{nodePath}' not found.\", nameof(nodePath));\n" +
            "\n" +
            "                node.Name = newName;\n" +
            "                return $\"Renamed to '{newName}'.\";\n" +
            "            });\n" +
            "        }\n" +
            "    }\n" +
            "}\n" +
            "#endif\n" +
            "```\n\n" +
            "## Suggestions\n\n" +
            "### Always marshal Godot API calls onto the main thread\n" +
            "Tool handlers run on a background SignalR thread. EVERY touch of a `Node`, `Resource`, " +
            "`SceneTree`, or `EditorInterface` must go through `MainThread.Instance.Run(() => { ... })` " +
            "(`com.IvanMurzak.ReflectorNet.Utils.MainThread`) — off-thread access crashes the editor rather " +
            "than throwing.\n\n" +
            "### Guard editor-only code with `#if TOOLS`\n" +
            "A tool that touches `EditorInterface`/`EditorPlugin`/`EditorFileSystem` must be wrapped in " +
            "`#if TOOLS ... #endif` so it is stripped from an exported game build (where those APIs do not " +
            "exist). A tool that only needs pure-managed state (like `ping`) should stay unguarded.\n\n" +
            "### Return structured data, not formatted strings\n" +
            "Prefer returning a data model (ReflectorNet serializes it for the client) over hand-formatted " +
            "text, so the AI can read individual fields. Return `void` for side-effect-only operations.\n\n" +
            "### Validate inputs first and throw clearly\n" +
            "Validate required parameters at the top of the method, before any Godot API call, and throw " +
            "`ArgumentException`/`InvalidOperationException` with a message that tells the AI how to " +
            "self-correct.\n\n" +
            "### Follow the addon's file conventions\n" +
            "One `[AiToolType] partial class Tool_<Family>` per family, one tool method per partial-class " +
            "file (`Tool_<Family>.<Method>.cs`), a `public const string <Name>ToolId` per tool, the " +
            "Apache-2.0 header at the top of every file, `#nullable enable`, Allman braces, and 4-space " +
            "indentation.";
    }
}
