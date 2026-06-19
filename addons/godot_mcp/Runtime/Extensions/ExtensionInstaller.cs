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

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>The outcome of an <see cref="ExtensionInstaller.Install"/> attempt — drives the dock's status line.</summary>
    public enum ExtensionInstallOutcome
    {
        /// <summary>The reference was added — the consumer must REBUILD solutions to restore + compile the new package.</summary>
        Added,

        /// <summary>The reference's version was bumped — the consumer must REBUILD solutions to restore the new version.</summary>
        Updated,

        /// <summary>Already up to date — no write was performed.</summary>
        AlreadyUpToDate,

        /// <summary>No consumer <c>.csproj</c> could be located — nothing was written.</summary>
        NoProjectFile,

        /// <summary>The <c>.csproj</c> could not be read / parsed / written — nothing was changed (see <see cref="ExtensionInstallResult.Message"/>).</summary>
        Failed
    }

    /// <summary>The result of an install attempt: the <see cref="Outcome"/>, a human-readable <see cref="Message"/>, and whether a rebuild is now needed.</summary>
    /// <param name="Outcome">The classified outcome.</param>
    /// <param name="Message">A short status message safe to show in the dock (never contains secrets).</param>
    /// <param name="RebuildRequired">True when a <c>.csproj</c> write happened, so the consumer must rebuild solutions (Godot has no programmatic restore).</param>
    public sealed record ExtensionInstallResult(ExtensionInstallOutcome Outcome, string Message, bool RebuildRequired);

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) orchestrator that performs an extension install by
    /// composing the <see cref="ExtensionInstallPlanner"/> (pure transform) with an injected
    /// <see cref="IConsumerProjectFile"/> (the IO seam): read current <c>.csproj</c> → plan add/update/no-op → write
    /// the resulting text → return a classified <see cref="ExtensionInstallResult"/> that tells the dock whether a
    /// rebuild is now required. Because the IO is an injected interface, the WHOLE flow (including the
    /// rebuild-needed signalling and the no-op short-circuit) is unit-tested with an in-memory fake — no Godot, no
    /// filesystem. The editor wiring just constructs a real <c>ConsumerProjectFile</c> and surfaces the message.
    /// </summary>
    public static class ExtensionInstaller
    {
        /// <summary>
        /// Install (or update) <paramref name="descriptor"/> into the consumer project behind <paramref name="file"/>.
        /// Never throws on an expected failure (missing/unreadable/unwritable/invalid <c>.csproj</c>) — those map to
        /// <see cref="ExtensionInstallOutcome.NoProjectFile"/> / <see cref="ExtensionInstallOutcome.Failed"/> with a
        /// safe message. A genuine no-op (already up to date) performs no write.
        /// </summary>
        public static ExtensionInstallResult Install(GodotExtensionDescriptor descriptor, IConsumerProjectFile file)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (!file.Exists)
                return new ExtensionInstallResult(
                    ExtensionInstallOutcome.NoProjectFile,
                    "No project .csproj was found to install into.",
                    RebuildRequired: false);

            var current = file.Read();
            if (current == null)
                return new ExtensionInstallResult(
                    ExtensionInstallOutcome.Failed,
                    "Could not read the project .csproj.",
                    RebuildRequired: false);

            ExtensionInstallPlan plan;
            try
            {
                plan = ExtensionInstallPlanner.Plan(descriptor, current);
            }
            catch (ArgumentException ex)
            {
                // Unparseable / malformed .csproj — refuse to write, surface the reason.
                return new ExtensionInstallResult(
                    ExtensionInstallOutcome.Failed,
                    $"Could not edit the project .csproj: {ex.Message}",
                    RebuildRequired: false);
            }

            if (!plan.RequiresWrite)
                return new ExtensionInstallResult(
                    ExtensionInstallOutcome.AlreadyUpToDate,
                    $"{descriptor.Name} is already installed and up to date.",
                    RebuildRequired: false);

            if (!file.Write(plan.ResultingCsproj))
                return new ExtensionInstallResult(
                    ExtensionInstallOutcome.Failed,
                    "Could not write the updated project .csproj.",
                    RebuildRequired: false);

            return plan.Action == ExtensionInstallAction.Add
                ? new ExtensionInstallResult(
                    ExtensionInstallOutcome.Added,
                    $"Added {descriptor.PackageId}"
                        + (descriptor.HasVersion ? $" {descriptor.Version}" : string.Empty)
                        + ". Rebuild solutions to restore and compile the extension.",
                    RebuildRequired: true)
                : new ExtensionInstallResult(
                    ExtensionInstallOutcome.Updated,
                    $"Updated {descriptor.PackageId} to {plan.ToVersion}. Rebuild solutions to restore the new version.",
                    RebuildRequired: true);
        }
    }
}
