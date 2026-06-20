# ┌──────────────────────────────────────────────────────────────────┐
# │  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
# │  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
# │  Copyright (c) 2026 Ivan Murzak                                  │
# │  Licensed under the Apache License, Version 2.0.                 │
# │  See the LICENSE file in the project root for more information.  │
# └──────────────────────────────────────────────────────────────────┘
#
# CI runtime-harness fault generator (issue #186). Raises a GENUINE GDScript
# RUNTIME error from inside a small CALL CHAIN so the engine produces a deep,
# multi-frame backtrace (issue #163) — NOT a parse error and NOT a push_error.
# The chain is `raise_runtime_fault -> _level_two -> _level_three`, and the
# innermost frame calls a NONEXISTENT method on a freshly-constructed object.
# Calling an undefined method is a real engine runtime fault ("Invalid call.
# Nonexistent function ..."), surfaced through Godot 4.5's OS.AddLogger error
# stream WITH a ScriptBacktrace, which the addon's GodotScriptErrorLogger
# materializes into RuntimeError.Frames. On Godot < 4.5 the engine logger
# channel is absent, so this same fault is simply not captured (the C# channels
# still are) — which is exactly the per-version degradation the harness asserts.
#
# This is driven from C# (RuntimeHarness.cs) via `call("raise_runtime_fault")`
# so the harness can sequence it deterministically AFTER capture is installed.
extends RefCounted


# Public entry point the C# harness invokes. Kept as the OUTERMOST frame of the
# backtrace so the captured stack has a recognizable top function name.
func raise_runtime_fault() -> void:
	_level_two()


func _level_two() -> void:
	_level_three()


func _level_three() -> void:
	# A real runtime fault with a multi-frame stack: invoke a method that does
	# not exist on a live object. The engine raises
	#   "Invalid call. Nonexistent function 'this_method_does_not_exist' in base 'RefCounted'."
	# at runtime (this line compiles fine — the call is resolved dynamically),
	# producing a ScriptBacktrace spanning _level_three -> _level_two ->
	# raise_runtime_fault.
	var obj := RefCounted.new()
	obj.call("this_method_does_not_exist", 1, 2, 3)
