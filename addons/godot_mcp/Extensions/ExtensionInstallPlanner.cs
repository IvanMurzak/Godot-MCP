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
using System.Linq;
using System.Xml.Linq;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>The action an <see cref="ExtensionInstallPlan"/> represents.</summary>
    public enum ExtensionInstallAction
    {
        /// <summary>No <c>&lt;PackageReference&gt;</c> exists for the package → add one.</summary>
        Add,

        /// <summary>A reference exists but pins an older version → bump its version to the descriptor's.</summary>
        Update,

        /// <summary>A reference already exists at the right version → nothing to write.</summary>
        NoOp
    }

    /// <summary>
    /// The result of <see cref="ExtensionInstallPlanner.Plan"/>: what to do (<see cref="Action"/>) and the resulting
    /// <c>.csproj</c> text (<see cref="ResultingCsproj"/>) the editor IO writes back. For a
    /// <see cref="ExtensionInstallAction.NoOp"/> the resulting text equals the original (the IO can skip the write).
    /// Pure value object — no Godot types, no IO.
    /// </summary>
    /// <param name="Action">The add / update / no-op decision.</param>
    /// <param name="ResultingCsproj">The full <c>.csproj</c> text after applying the action (== original for no-op).</param>
    /// <param name="PackageId">The package id the plan targets (echoed for the UI / status line).</param>
    /// <param name="FromVersion">The version currently referenced (null when not installed; empty string when referenced without a version).</param>
    /// <param name="ToVersion">The descriptor's target version (null when the descriptor has no version pin).</param>
    public sealed record ExtensionInstallPlan(
        ExtensionInstallAction Action,
        string ResultingCsproj,
        string PackageId,
        string? FromVersion,
        string? ToVersion)
    {
        /// <summary>True when applying this plan changes the <c>.csproj</c> (i.e. not a <see cref="ExtensionInstallAction.NoOp"/>).</summary>
        public bool RequiresWrite => Action != ExtensionInstallAction.NoOp;
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>, no IO) planner that, given a
    /// <see cref="GodotExtensionDescriptor"/> and the CURRENT consumer <c>.csproj</c> text, computes the
    /// install/update/no-op plan AND the resulting <c>.csproj</c> text — the way <c>AgentConfigJson</c> computes a
    /// config edit as a pure transform. <b>This is the key tested seam.</b> The editor IO (locating + reading +
    /// writing the consumer <c>.csproj</c>) is a thin shell over this; the transform itself is deterministic and
    /// unit-tested.
    ///
    /// <para>
    /// The transform is done with <see cref="System.Xml.Linq"/> so it is identical on every host OS (no
    /// platform-dependent path/rooting logic). It PRESERVES every other <c>&lt;PackageReference&gt;</c>, every other
    /// <c>ItemGroup</c>, and all unrelated XML — only the targeted package's reference is added or its
    /// <c>Version</c> attribute bumped. A brand-new reference is appended to the FIRST <c>ItemGroup</c> that already
    /// holds a <c>&lt;PackageReference&gt;</c> (so it joins the existing package list); if the project has none, a
    /// fresh <c>&lt;ItemGroup&gt;</c> is appended to the root.
    /// </para>
    /// </summary>
    public static class ExtensionInstallPlanner
    {
        /// <summary>
        /// Compute the install plan for <paramref name="descriptor"/> against <paramref name="csprojText"/>.
        /// <list type="bullet">
        ///   <item>No reference present → <see cref="ExtensionInstallAction.Add"/> (append a new one).</item>
        ///   <item>
        ///     Reference present but older than the descriptor's pinned version (or present with no version while the
        ///     descriptor pins one) → <see cref="ExtensionInstallAction.Update"/> (set the <c>Version</c> attribute).
        ///   </item>
        ///   <item>Reference present and already up to date (or descriptor has no version pin) → <see cref="ExtensionInstallAction.NoOp"/>.</item>
        /// </list>
        /// Throws <see cref="ArgumentException"/> only on a genuinely unparseable <c>.csproj</c> (the editor surfaces
        /// that as an error rather than silently corrupting the project file — distinct from the DETECTOR, which
        /// tolerates bad XML as "nothing installed").
        /// </summary>
        public static ExtensionInstallPlan Plan(GodotExtensionDescriptor descriptor, string csprojText)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (csprojText == null)
                throw new ArgumentNullException(nameof(csprojText));

            XDocument doc;
            try
            {
                doc = XDocument.Parse(csprojText, LoadOptions.PreserveWhitespace);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new ArgumentException(
                    "The consumer .csproj is not valid XML; refusing to edit it.", nameof(csprojText), ex);
            }

            if (doc.Root == null)
                throw new ArgumentException("The consumer .csproj has no root element.", nameof(csprojText));

            var existing = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "PackageReference"
                    && string.Equals((string?)e.Attribute("Include"), descriptor.PackageId, StringComparison.OrdinalIgnoreCase));

            // --- No existing reference → ADD ---
            if (existing == null)
            {
                AppendPackageReference(doc, descriptor);
                return new ExtensionInstallPlan(
                    ExtensionInstallAction.Add,
                    Serialize(doc),
                    descriptor.PackageId,
                    FromVersion: null,
                    ToVersion: descriptor.HasVersion ? descriptor.Version : null);
            }

            // --- Existing reference → decide UPDATE vs NO-OP ---
            var currentVersion = ReadReferenceVersion(existing);

            // Descriptor pins no version → leave the existing reference untouched (no target to compare to).
            if (!descriptor.HasVersion)
            {
                return new ExtensionInstallPlan(
                    ExtensionInstallAction.NoOp, csprojText, descriptor.PackageId, currentVersion, ToVersion: null);
            }

            var target = descriptor.Version!;
            var needsBump = string.IsNullOrEmpty(currentVersion)
                || InstalledStateDetector.CompareVersions(target, currentVersion) > 0;

            if (!needsBump)
            {
                return new ExtensionInstallPlan(
                    ExtensionInstallAction.NoOp, csprojText, descriptor.PackageId, currentVersion, target);
            }

            SetReferenceVersion(existing, target);
            return new ExtensionInstallPlan(
                ExtensionInstallAction.Update,
                Serialize(doc),
                descriptor.PackageId,
                currentVersion,
                target);
        }

        // --- internals --------------------------------------------------------------------------------------

        /// <summary>Read the version off an existing <c>&lt;PackageReference&gt;</c> (attribute form, else child element). Empty string when unversioned.</summary>
        static string ReadReferenceVersion(XElement reference)
        {
            var attr = (string?)reference.Attribute("Version");
            if (!string.IsNullOrEmpty(attr))
                return attr!.Trim();

            var child = reference.Elements().FirstOrDefault(e => e.Name.LocalName == "Version");
            return (child?.Value ?? string.Empty).Trim();
        }

        /// <summary>Set the version on an existing reference, honouring whichever form it already uses (child element vs attribute).</summary>
        static void SetReferenceVersion(XElement reference, string version)
        {
            var child = reference.Elements().FirstOrDefault(e => e.Name.LocalName == "Version");
            if (child != null)
            {
                child.Value = version;
                return;
            }

            reference.SetAttributeValue("Version", version);
        }

        /// <summary>
        /// Append a new <c>&lt;PackageReference&gt;</c> for the descriptor, in the SAME XML namespace as the root (so
        /// the element is valid whether or not the project uses an explicit MSBuild namespace). It joins the first
        /// <c>ItemGroup</c> that already contains a <c>&lt;PackageReference&gt;</c>; if none exists, a fresh
        /// <c>&lt;ItemGroup&gt;</c> is appended to the root.
        /// </summary>
        static void AppendPackageReference(XDocument doc, GodotExtensionDescriptor descriptor)
        {
            var ns = doc.Root!.Name.Namespace;

            var reference = new XElement(ns + "PackageReference",
                new XAttribute("Include", descriptor.PackageId));
            if (descriptor.HasVersion)
                reference.SetAttributeValue("Version", descriptor.Version);

            var targetGroup = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "ItemGroup"
                    && e.Elements().Any(c => c.Name.LocalName == "PackageReference"));

            if (targetGroup == null)
            {
                targetGroup = new XElement(ns + "ItemGroup");
                doc.Root.Add(targetGroup);
            }

            targetGroup.Add(reference);
        }

        /// <summary>
        /// Serialize the document back to text. Disables the XML declaration (SDK-style csproj files have none) and
        /// uses <see cref="SaveOptions.DisableFormatting"/> so the preserved whitespace (loaded with
        /// <see cref="LoadOptions.PreserveWhitespace"/>) is emitted verbatim rather than re-indented — keeping the
        /// diff minimal and untouched siblings byte-stable.
        /// </summary>
        static string Serialize(XDocument doc)
        {
            using var writer = new System.IO.StringWriter();
            var settings = new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                // Emit a trailing newline-free body; PreserveWhitespace already carries the source's own newlines.
                NewLineHandling = System.Xml.NewLineHandling.None
            };
            using (var xmlWriter = System.Xml.XmlWriter.Create(writer, settings))
            {
                doc.Save(xmlWriter);
            }
            return writer.ToString();
        }
    }
}
