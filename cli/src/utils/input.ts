import * as fs from 'fs';
import * as path from 'path';
import { verbose } from './ui.js';
import { parseJsonStrict, JsonParseError } from './json-parse.js';
import { ExitCode } from './exit-codes.js';
import { exitWithError } from './output.js';

export interface InputOptions {
  input?: string;
  inputFile?: string;
}

function usage(message: string): never {
  return exitWithError(ExitCode.Usage, 'USAGE', message);
}

/**
 * Parse tool input from --input, --input-file, or stdin. Bad input is a usage error (exit 2).
 *
 * - `--input-file -` or `--input-file /dev/stdin` reads from stdin (cross-platform).
 * - `--input-file <path>` reads from a file.
 * - `--input <json>` parses inline JSON.
 * - Falls back to `{}` if nothing provided.
 */
export function parseInput(options: InputOptions): string {
  if (options.inputFile) {
    const isStdin = options.inputFile === '-' || options.inputFile === '/dev/stdin';
    let content: string;

    if (isStdin) {
      verbose('Reading input from stdin...');
      try {
        content = fs.readFileSync(0, 'utf-8');
      } catch {
        usage('Failed to read from stdin.');
      }
      if (!content.trim()) usage('No input received from stdin.');
    } else {
      const filePath = path.resolve(options.inputFile);
      if (!fs.existsSync(filePath)) usage(`Input file does not exist: ${filePath}`);
      content = fs.readFileSync(filePath, 'utf-8');
    }

    try {
      parseJsonStrict(content);
    } catch (err) {
      const source = isStdin ? 'stdin' : options.inputFile;
      usage(`--input-file content from '${source}' must be valid JSON` + (err instanceof JsonParseError ? `\n${err.message}` : ''));
    }

    return content;
  }

  if (options.input) {
    try {
      const result = parseJsonStrict(options.input);
      return result.raw;
    } catch (err) {
      usage('--input must be valid JSON' + (err instanceof JsonParseError ? `\n${err.message}` : ''));
    }
  }

  return '{}';
}

/** Commander accumulator for a repeatable option. */
export function collect(value: string, previous: string[]): string[] {
  return [...previous, value];
}
