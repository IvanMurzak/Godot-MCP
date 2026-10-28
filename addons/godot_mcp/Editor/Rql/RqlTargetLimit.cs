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
    /// <see cref="Ceiling"/>. RQL refuses a value outside 1 … <see cref="Ceiling"/> (invalid-value, nothing runs); an omitted
    /// argument sends no budget, so the host default applies. Pure (no Godot API): unit-tested over a real engine.
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

        /// <summary>The request budgets for a <see cref="Argument"/> value: none when it was not given (never 0 for "unset").</summary>
        public static Budgets? BudgetsFor(int? maxTargets) => maxTargets is int n ? new Budgets { MaxMutationTargets = n } : null;
    }
}
#endif
