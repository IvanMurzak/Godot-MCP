/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// How many objects one rql-mutate set / call stage may change (RQL spec §8 "Target limit"). The limit guards the breadth
    /// of a change, not its correctness (all-or-nothing, rollback, dry runs and the one undo action hold at any size), so the
    /// editor default is <see cref="Default"/>; a request raises it with the <see cref="Argument"/> tool argument up to
    /// <see cref="Ceiling"/>. Pure (no Godot API): unit-tested over a real engine.
    /// </summary>
    internal static class RqlTargetLimit
    {
        /// <summary>The rql-mutate argument that sets the limit; RQL's mutation-limit hint names it.</summary>
        public const string Argument = "max_targets";

        public const int Default = 200;

        public const int Ceiling = 1000;

        public static void Configure(MutationPolicy mutations)
        {
            mutations.MaxTargets = Default;
            mutations.HardMaxTargets = Ceiling;
            mutations.LimitArgument = Argument;
        }

        /// <summary>The request budgets for a <see cref="Argument"/> value.</summary>
        public static Budgets BudgetsFor(int maxTargets) => new Budgets { MaxMutationTargets = maxTargets };

        /// <summary>Why a <see cref="Argument"/> value is refused (outside 1 … <see cref="Ceiling"/>), or null.</summary>
        public static string? Problem(int maxTargets) =>
            maxTargets < 1 || maxTargets > Ceiling ? $"{Argument} must be between 1 and {Ceiling} (got {maxTargets}); nothing was changed" : null;

        /// <summary>What to do instead of a refused <see cref="Argument"/> value.</summary>
        public static string ProblemHint(int maxTargets) =>
            maxTargets < 1
                ? $"pass a value from 1 to {Ceiling}, or leave {Argument} out for the default {Default}"
                : $"change more objects in batches: repeat … | first {Ceiling} | set … while the where still matches unchanged objects, or page " +
                  $"with … | skip {Ceiling} | first {Ceiling} | set … when the change does not affect the where";
    }
}
#endif
