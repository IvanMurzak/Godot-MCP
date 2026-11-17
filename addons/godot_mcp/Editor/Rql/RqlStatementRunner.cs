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
using System;
using System.Collections.Generic;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Runs a change of several statements (<c>;</c>, RQL spec §14) one statement at a time: after each statement that ran —
    /// applied, failed or rolled back — <c>finished</c> gets its text, where the editor commits the statement's undo action and
    /// drops what it cached, so the next statement sees the earlier one as a finished change. Pure managed (unit-tested).
    /// </summary>
    internal static class RqlStatementRunner
    {
        public static QueryResult Run(IReadOnlyList<RqlStatement> statements, QueryOptions options, Func<QueryPlan, QueryResult> execute,
            Action<string> finished)
        {
            // A change runs its statements in order and stops at the first failure (a read skips the ones that do not parse),
            // so here the n-th plan is the n-th statement.
            if (!options.AllowMutation) throw new ArgumentException("only a change runs statement by statement", nameof(options));
            var next = 0;
            return RqlScript.Run(statements, options, plan =>
            {
                var text = statements[next++].Text;
                try { return execute(plan); }
                finally { finished(text); }
            });
        }
    }
}
#endif
