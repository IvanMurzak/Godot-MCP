/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Godot's two first-class scripting languages, distinguished by file extension. <see cref="CSharp"/>
    /// (<c>.cs</c>) compiles into the project assembly (a build/reload is needed before a freshly-added
    /// type is usable); <see cref="GDScript"/> (<c>.gd</c>) is interpreted and re-parsed on load.
    /// </summary>
    public enum ScriptLang
    {
        CSharp,
        GDScript,
    }

    /// <summary>
    /// Pure-string helpers shared by the script tool family (<c>script-*</c>): extension → language
    /// detection and <c>res://</c> script-path validation. Extracted from the editor-only
    /// (<c>#if TOOLS</c>) handlers — like <see cref="ResPathNormalizer"/> / <see cref="NodePathNormalizer"/>
    /// — so the extension/validation logic is unit-testable in the plain xUnit host with no live Godot
    /// scripting runtime.
    ///
    /// <para>
    /// A "script" here is a source file Godot recognizes as a <see cref="global::Godot.Script"/>: a C#
    /// (<c>.cs</c>) or GDScript (<c>.gd</c>) file under <c>res://</c>. The editor-side handlers additionally
    /// re-use <see cref="ResPathNormalizer.RequireResFilePath"/> for the shared <c>res://</c> + <c>..</c>
    /// + bare-scheme guards; this class layers the language-specific extension contract on top.
    /// </para>
    /// </summary>
    public static class ScriptLang_
    {
        public const string CSharpExtension = ".cs";
        public const string GDScriptExtension = ".gd";

        /// <summary>
        /// True when <paramref name="path"/> ends with a recognized script extension (<c>.cs</c> or
        /// <c>.gd</c>), case-insensitively. Does NOT validate the <c>res://</c> scheme — pair with
        /// <see cref="ResPathNormalizer.IsResPath"/> / <see cref="RequireScriptResPath"/> for that.
        /// </summary>
        public static bool IsScriptExtension(string? path)
            => TryGetLang(path, out _);

        /// <summary>
        /// Resolve a script file path to its <see cref="ScriptLang"/> by extension. Returns false (and
        /// <paramref name="lang"/> = <see cref="ScriptLang.CSharp"/> as an unused default) when the path
        /// has no recognized script extension.
        /// </summary>
        public static bool TryGetLang(string? path, out ScriptLang lang)
        {
            lang = ScriptLang.CSharp;
            if (string.IsNullOrEmpty(path))
                return false;

            if (path!.EndsWith(CSharpExtension, StringComparison.OrdinalIgnoreCase))
            {
                lang = ScriptLang.CSharp;
                return true;
            }
            if (path.EndsWith(GDScriptExtension, StringComparison.OrdinalIgnoreCase))
            {
                lang = ScriptLang.GDScript;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The canonical lowercase extension (including the leading dot) for a language.
        /// </summary>
        public static string ExtensionFor(ScriptLang lang)
            => lang == ScriptLang.GDScript ? GDScriptExtension : CSharpExtension;

        /// <summary>
        /// Validate that <paramref name="path"/> is a <c>res://</c> script file path (a file under
        /// <c>res://</c>, not the bare root or a directory, with no <c>..</c> segment, ending in
        /// <c>.cs</c>/<c>.gd</c>), throwing <see cref="ArgumentException"/> (with
        /// <paramref name="paramName"/>) otherwise. Returns the trimmed path and its detected language on
        /// success. Layers the script-extension contract on top of
        /// <see cref="ResPathNormalizer.RequireResFilePath"/> so the two share one set of res:// guards.
        /// </summary>
        public static string RequireScriptResPath(string? path, string paramName, out ScriptLang lang)
        {
            var p = ResPathNormalizer.RequireResFilePath(path, paramName);
            if (!TryGetLang(p, out lang))
                throw new ArgumentException(
                    $"Script path must end with '{CSharpExtension}' (C#) or '{GDScriptExtension}' (GDScript); got '{path}'.",
                    paramName);
            return p;
        }
    }
}
