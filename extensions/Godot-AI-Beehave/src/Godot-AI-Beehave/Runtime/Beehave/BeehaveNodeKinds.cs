/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// Pure-managed parsing that maps an LLM-supplied kind string ("selector" / "sequence", "inverter" /
    /// "limiter", "action" / "condition") onto the Beehave <c>class_name</c> the editor tool must instantiate
    /// by string. CI-unit-testable (no Godot binary): this is the Class-B analog of Particles' dimension
    /// parsing, the layer that keeps the editor tools thin and pins the addon class names through
    /// <see cref="BeehaveEnums"/>.
    /// </summary>
    public static class BeehaveNodeKinds
    {
        // ── Composites ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve a composite kind string to its Beehave <c>class_name</c>. Accepts (case/whitespace-
        /// insensitive): <c>"selector"</c>/<c>"sel"</c> → <see cref="BeehaveEnums.SelectorClass"/>;
        /// <c>"sequence"</c>/<c>"seq"</c> → <see cref="BeehaveEnums.SequenceClass"/>. Throws on anything else.
        /// </summary>
        public static string ResolveCompositeClass(string? kind)
        {
            switch (Normalize(kind))
            {
                case "selector":
                case "sel":
                    return BeehaveEnums.SelectorClass;
                case "sequence":
                case "seq":
                    return BeehaveEnums.SequenceClass;
                default:
                    throw new ArgumentException(
                        $"Unrecognized composite kind '{kind}'. Use 'selector' or 'sequence'.", nameof(kind));
            }
        }

        // ── Decorators ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve a decorator kind string to its Beehave <c>class_name</c>. Accepts (case/whitespace-
        /// insensitive): <c>"inverter"</c>/<c>"invert"</c> → <see cref="BeehaveEnums.InverterClass"/>;
        /// <c>"limiter"</c>/<c>"limit"</c> → <see cref="BeehaveEnums.LimiterClass"/>. Throws on anything else.
        /// </summary>
        public static string ResolveDecoratorClass(string? kind)
        {
            switch (Normalize(kind))
            {
                case "inverter":
                case "invert":
                    return BeehaveEnums.InverterClass;
                case "limiter":
                case "limit":
                    return BeehaveEnums.LimiterClass;
                default:
                    throw new ArgumentException(
                        $"Unrecognized decorator kind '{kind}'. Use 'inverter' or 'limiter'.", nameof(kind));
            }
        }

        // ── Leaves ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve a leaf kind string to its Beehave <c>class_name</c>. Accepts (case/whitespace-insensitive):
        /// <c>"action"</c> → <see cref="BeehaveEnums.ActionLeafClass"/>; <c>"condition"</c>/<c>"cond"</c> →
        /// <see cref="BeehaveEnums.ConditionLeafClass"/>. Throws on anything else. Beehave leaves are abstract —
        /// the tool only scaffolds the base node; the user attaches the real behaviour script.
        /// </summary>
        public static string ResolveLeafClass(string? kind)
        {
            switch (Normalize(kind))
            {
                case "action":
                    return BeehaveEnums.ActionLeafClass;
                case "condition":
                case "cond":
                    return BeehaveEnums.ConditionLeafClass;
                default:
                    throw new ArgumentException(
                        $"Unrecognized leaf kind '{kind}'. Use 'action' or 'condition'.", nameof(kind));
            }
        }

        static string Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim().ToLowerInvariant();
    }
}
