/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    public partial class Tool_Dialogic
    {
        /// <summary>
        /// Editor-only tool — create + save a Dialogic character resource (<c>.dch</c>) at a <c>res://</c> path
        /// with a display name, optional color, and optional description. Presence-gated; main-thread-marshalled.
        /// The Dialogic <c>DialogicCharacter</c> type is resolved by string name and its members are set via the
        /// GDScript snake_case property names (the package never names a Dialogic type).
        /// </summary>
        [AiTool
        (
            CharacterCreateToolId,
            Title = "Dialogic / Character Create"
        )]
        [Description("Create a Dialogic character resource (.dch) at the given res:// path. '.dch' is appended to " +
            "'resourcePath' if missing. 'displayName' is required; 'color' is an optional '#rrggbb' hex (default a " +
            "friendly blue) and 'description' is optional. Returns the structured result (Installed, ResourcePath, " +
            "DisplayName, Color, Description). If the Dialogic addon is not installed, returns Installed:false with " +
            "an install hint instead of crashing.")]
        public DialogicResourceInfo CharacterCreate
        (
            [Description("res:// path for the new character, e.g. 'res://characters/hero.dch' ('.dch' appended if missing).")]
            string resourcePath,
            [Description("The character's display name (required).")]
            string displayName,
            [Description("Optional color as '#rrggbb' hex (e.g. '#e91e63'). Invalid/empty falls back to a default blue.")]
            string? color = null,
            [Description("Optional character description.")]
            string? description = null
        )
        {
            var hex = DialogicColor.NormalizeHex(color);

            return MainThread.Instance.Run(() =>
            {
                var gate = GateInstalled();
                if (gate != null) return gate;

                var path = NormalizeCharacterPath(resourcePath);

                var character = AddonInterop.InstantiateScriptResource(DialogicAddon.CharacterClass)
                    ?? throw new System.InvalidOperationException(
                        $"Could not instantiate '{DialogicAddon.CharacterClass}' — is the Dialogic addon installed?");

                character.Set(DialogicAddon.DisplayNameMember, displayName ?? string.Empty);
                character.Set(DialogicAddon.ColorMember, Color.FromHtml(hex));
                character.Set(DialogicAddon.DescriptionMember, description ?? string.Empty);

                var err = ResourceSaver.Save(character, path);
                if (err != Error.Ok)
                    throw new System.InvalidOperationException($"ResourceSaver.Save failed for '{path}': {err}.");

                return new DialogicResourceInfo
                {
                    Installed = true,
                    ResourceKind = DialogicResourceKind.Character,
                    ResourcePath = path,
                    DisplayName = displayName ?? string.Empty,
                    Color = DialogicColor.ToDisplay(hex),
                    Description = description ?? string.Empty,
                };
            });
        }
    }
}
#endif
