// Process exit codes shared by godot-cli, unity-mcp-cli and unreal-mcp-cli (keep the three identical).

export const ExitCode = {
  Success: 0,
  Error: 1,
  Usage: 2,
  CommandFailed: 6,
  Unreachable: 7,
  TestsFailed: 8,
  Timeout: 14,
  Busy: 17,
  AwaitingInput: 18,
} as const;

export type ExitCodeValue = (typeof ExitCode)[keyof typeof ExitCode];

/** The exit-code table `godot-cli commands` publishes. */
export const EXIT_CODE_TABLE: ReadonlyArray<{ code: ExitCodeValue; name: keyof typeof ExitCode; description: string }> = [
  { code: ExitCode.Success, name: 'Success', description: 'Success' },
  { code: ExitCode.Error, name: 'Error', description: 'Generic or unclassified error' },
  { code: ExitCode.Usage, name: 'Usage', description: 'Usage error: unknown command, bad flag or argument' },
  { code: ExitCode.CommandFailed, name: 'CommandFailed', description: 'The command ran and failed (tool error, script or compilation errors, no test verdict)' },
  { code: ExitCode.Unreachable, name: 'Unreachable', description: 'The editor or MCP server is unreachable' },
  { code: ExitCode.TestsFailed, name: 'TestsFailed', description: 'Tests ran and at least one failed' },
  { code: ExitCode.Timeout, name: 'Timeout', description: 'Timed out' },
  { code: ExitCode.Busy, name: 'Busy', description: 'The server is busy (HTTP 503); the call was not executed' },
  { code: ExitCode.AwaitingInput, name: 'AwaitingInput', description: 'The editor is blocked by a modal dialog (where detectable)' },
];
