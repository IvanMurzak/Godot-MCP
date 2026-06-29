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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Extensions;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed CLASS-B "requires the &lt;X&gt; addon" surfacing helpers on
    /// <see cref="ExtensionsPanelText"/> (the note string + the AssetLib/repo link derivation the dock's
    /// <see cref="ExtensionsPanelText"/>-driven <c>ExtensionRow</c> shows). The editor Control wiring
    /// (<c>ExtensionRow.cs</c>, <c>#if TOOLS</c>) is verified via the headless Godot smoke (test.md Suite 3);
    /// the text + URL logic is unit-tested here so the addon-required surfacing never silently drops.
    /// </summary>
    public class ExtensionsPanelTextTests
    {
        [Fact]
        public void AddonRequiredNotice_IncludesTheAddonName()
        {
            var addon = new GodotAddonRequirement("Phantom Camera", "1822", "ramokz/phantom-camera", "MIT");
            var notice = ExtensionsPanelText.AddonRequiredNotice(addon);
            Assert.Contains("Phantom Camera", notice);
            Assert.Contains("addon", notice);
        }

        [Fact]
        public void AddonRequiredUrl_PrefersAssetLib_WhenPresent()
        {
            // AssetLib is the in-editor install location, so it wins over the repo when an id is known.
            var addon = new GodotAddonRequirement("Phantom Camera", "1822", "ramokz/phantom-camera", "MIT");
            Assert.Equal("https://godotengine.org/asset-library/asset/1822", ExtensionsPanelText.AddonRequiredUrl(addon));
        }

        [Fact]
        public void AddonRequiredUrl_FallsBackToRepo_ExpandsBareOwnerNameToGithub()
        {
            // No AssetLib id (Dialogic's shape) → a bare owner/name repo is expanded to a GitHub URL.
            var addon = new GodotAddonRequirement("Dialogic", AssetLibId: null, Repo: "dialogic-godot/dialogic", License: "MIT");
            Assert.Equal("https://github.com/dialogic-godot/dialogic", ExtensionsPanelText.AddonRequiredUrl(addon));
        }

        [Fact]
        public void AddonRequiredUrl_AbsoluteHttpRepo_UsedVerbatim()
        {
            var addon = new GodotAddonRequirement("Some Addon", AssetLibId: null, Repo: "https://example.com/some/addon", License: null);
            Assert.Equal("https://example.com/some/addon", ExtensionsPanelText.AddonRequiredUrl(addon));
        }

        [Fact]
        public void AddonRequiredUrl_WhitespaceAssetLib_FallsBackToRepo()
        {
            // A blank assetLibId must not produce ".../asset/" — it falls through to the repo.
            var addon = new GodotAddonRequirement("Some Addon", AssetLibId: "   ", Repo: "owner/name", License: null);
            Assert.Equal("https://github.com/owner/name", ExtensionsPanelText.AddonRequiredUrl(addon));
        }

        [Fact]
        public void AddonRequiredUrl_NeitherAssetLibNorRepo_ReturnsNull()
        {
            var addon = new GodotAddonRequirement("Some Addon", AssetLibId: null, Repo: null, License: null);
            Assert.Null(ExtensionsPanelText.AddonRequiredUrl(addon));
        }

        [Fact]
        public void ShippedCatalog_EveryClassBExtension_SurfacesNoticeAndLink()
        {
            // Tie the helpers to the REAL embedded catalog: every Class-B (addon-dependent) extension that ships
            // must yield a note naming its required addon AND a resolvable link. The shipped catalog has Class-B
            // entries (PhantomCamera, Beehave, Dialogic, Terrain3D); assert there is at least one so this is meaningful.
            var classB = GodotExtensionRegistry.All.Where(d => d.RequiresAddon).ToList();
            Assert.NotEmpty(classB);

            foreach (var ext in classB)
            {
                Assert.NotNull(ext.AddonRequired);
                var notice = ExtensionsPanelText.AddonRequiredNotice(ext.AddonRequired!);
                Assert.Contains(ext.AddonRequired!.Name, notice);
                Assert.False(string.IsNullOrEmpty(ExtensionsPanelText.AddonRequiredUrl(ext.AddonRequired)));
            }
        }
    }
}
