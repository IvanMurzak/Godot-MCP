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
using System;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// One Dialogic character file (a row of <c>dialogic.characters</c>), read live from the loaded resource. Read-only;
    /// change a character through <c>resource</c> (<c>get resource | set props["display_name"] = "Hero"</c>): the core
    /// writes the property and saves the .dch file with Dialogic's saver.
    /// </summary>
    [RqlDescription("Dialogic character (read-only row): path name displayName color description resource; write through resource")]
    public sealed class DialogicCharacterRow
    {
        private readonly Lazy<Resource?> _resource;

        internal DialogicCharacterRow(string path)
        {
            this.path = path;
            _resource = new Lazy<Resource?>(() => ResourceLoader.Load(path));
        }

        [RqlDescription("res:// path of the .dch file")]
        public string path { get; }

        [RqlDisplay, RqlDescription("file name without the extension (the name a timeline's \"Name: text\" line uses)")]
        public string name => path.Substring(path.LastIndexOf('/') + 1).GetBaseName();

        [RqlDescription("display name (props[\"display_name\"])")]
        public string? displayName => resource?.Get(DialogicNames.DisplayName).AsString();

        [RqlDescription("name color as \"#rrggbb\" (props[\"color\"])")]
        public string? color => resource == null ? null : "#" + resource.Get(DialogicNames.Color).AsColor().ToHtml(false).ToLowerInvariant();

        [RqlDescription("description (props[\"description\"])")]
        public string? description => resource?.Get(DialogicNames.Description).AsString();

        [RqlReference, RqlDescription("the DialogicCharacter resource: write through it (get resource | set props[\"description\"] = \"…\"), the .dch is saved")]
        public Resource? resource => _resource.Value;
    }
}
#endif
