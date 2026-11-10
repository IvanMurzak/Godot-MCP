// `importProject`: a standalone `godot --headless --import` judged from its log (Godot exits 0 on import and script
// errors). Library-safe.

import type { Diagnostic } from './godot-log.js';
import { prepareHeadless, runImportStep, type HeadlessBaseOptions, type HeadlessResult, type HeadlessStep } from './headless.js';

export interface ImportReport {
  projectPath: string;
  steps: HeadlessStep[];
  diagnostics: Diagnostic[];
}

export type ImportResult = HeadlessResult<ImportReport>;

export async function importProject(options: HeadlessBaseOptions): Promise<ImportResult> {
  const ctx = prepareHeadless(options);
  if (ctx.kind === 'failure') return ctx;
  options.onProgress?.('Importing the project (godot --headless --import)...');
  const imported = await runImportStep(ctx);
  const report: ImportReport = { projectPath: ctx.projectPath, steps: ctx.steps, diagnostics: imported.diagnostics };
  if (imported.failure) return { ...imported.failure, ...report };
  return { kind: 'success', success: true, ...report };
}
