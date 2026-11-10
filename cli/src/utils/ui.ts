import chalk from 'chalk';
import yoctoSpinner, { type Spinner } from 'yocto-spinner';
import type { Command, Help } from 'commander';
import { CODE_COMMANDS, EDITOR_COMMANDS, EDITOR_START, ROOT_RQL_RECIPES, type CommandRow, type RqlRecipes } from '../lib/cli-guide.js';

export type { RqlRecipes };

// --- Verbose mode ---
let verboseEnabled = false;

export function setVerbose(enabled: boolean): void {
  verboseEnabled = enabled;
}

export function verbose(msg: string): void {
  if (verboseEnabled) {
    out(isTTY() ? chalk.dim(`[verbose] ${msg}`) : `[verbose] ${msg}`);
  }
}

// --- Machine output (--format json|ndjson): stdout carries only the envelope ---
let machineOutput = false;
let machineLine: ((level: 'info' | 'error', message: string) => string) | undefined;

/**
 * Route every human-readable line (and spinner) to stderr without colour, keeping stdout for the envelope.
 * `wrap` turns each line into an event (ndjson).
 */
export function setMachineOutput(enabled: boolean, wrap?: (level: 'info' | 'error', message: string) => string): void {
  machineOutput = enabled;
  machineLine = enabled ? wrap : undefined;
}

/** A human-readable line: stdout normally, plain (or wrapped) stderr in machine mode. */
function out(line: string, level: 'info' | 'error' = 'info'): void {
  if (!machineOutput) {
    if (level === 'error') console.error(line);
    else console.log(line);
    return;
  }
  const plain = stripAnsi(line);
  process.stderr.write((machineLine ? machineLine(level, plain) : plain) + '\n');
}

// --- TTY detection ---
function isTTY(): boolean {
  return !machineOutput && !!process.stdout.isTTY;
}

/**
 * Strip ANSI escape codes from a string.
 */
function stripAnsi(str: string): string {
  // eslint-disable-next-line no-control-regex
  return str.replace(/\x1B\[[0-9;]*m/g, '');
}

/**
 * The root help: short on purpose (agents read it first, and every later turn re-reads it): the everyday commands, the
 * other command names, and the RQL recipes most tasks need. Each command's own --help has the details.
 */
function formatRootHelp(target: Command, helper: Help, appVersion: string | undefined, recipes: RqlRecipes): string {
  const name = target.name();
  const featured = new Set([...EDITOR_COMMANDS, ...CODE_COMMANDS].map(([cmd]) => cmd));
  const row = ([, usage, what]: CommandRow) => `  ${chalk.yellow(usage.padEnd(26))} ${chalk.dim(what)}`;
  const others = helper.visibleCommands(target).map((c) => c.name()).filter((c) => !featured.has(c) && c !== 'help');
  const lines = [
    `${chalk.bold.cyan('Godot-MCP CLI')}${appVersion ? ` v${appVersion}` : ''}: drive the Godot editor of the project in the current folder (or --path <dir>).`,
    '',
    chalk.bold(`Editor work (${EDITOR_START}):`),
    ...EDITOR_COMMANDS.map(row),
    chalk.bold('Code:'),
    ...CODE_COMMANDS.map(row),
    `${chalk.bold('More commands:')} ${others.join(' ')}`,
  ];
  if (recipes.length > 0) {
    lines.push('', chalk.bold('RQL recipes (strings inside the query take double quotes):'));
    for (const [query, what] of recipes.slice(0, ROOT_RQL_RECIPES)) lines.push(`  ${chalk.yellow(`rql '${query}'`)}  ${chalk.dim(`# ${what}`)}`);
  }
  lines.push(
    '',
    chalk.dim(`Details: ${name} <command> --help (rql --help: operators, more recipes). Machine-readable output: --format json.`),
    '',
  );
  return lines.join('\n');
}

/**
 * Apply styled help formatting to a Commander command.
 * Call this on every Command instance (root and subcommands)
 * so that --help / -h always renders the fancy UI.
 *
 * @param appVersion - The CLI version string, shown in the root help.
 */
export function configureStyledHelp(cmd: Command, appVersion?: string, rootRecipes: RqlRecipes = []): Command {
  cmd.configureHelp({
    formatHelp(target: Command, helper: Help): string {
      if (!target.parent) return formatRootHelp(target, helper, appVersion, rootRecipes);
      const lines: string[] = [];

      // A plain line, not a box: agents read this help, and box borders cost as much as the text again.
      lines.push(`${chalk.bold.cyan(target.name())} ${chalk.dim('—')} ${target.description()}`);
      lines.push('');

      // Usage
      lines.push(`${chalk.bold('Usage:')} ${chalk.yellow(helper.commandUsage(target))}`);
      lines.push('');

      // Subcommands
      const subcommands = helper.visibleCommands(target);
      if (subcommands.length > 0) {
        lines.push(chalk.bold('Commands:'));
        for (const sub of subcommands) {
          lines.push(`  ${chalk.yellow(sub.name().padEnd(20))} ${chalk.dim(sub.description() || '')}`);
        }
        lines.push('');
      }

      // Arguments
      const args = helper.visibleArguments(target);
      if (args.length > 0) {
        lines.push(chalk.bold('Arguments:'));
        for (const arg of args) {
          lines.push(`  ${chalk.green(helper.argumentTerm(arg).padEnd(30))} ${chalk.dim(helper.argumentDescription(arg))}`);
        }
        lines.push('');
      }

      // Options
      const options = helper.visibleOptions(target);
      if (options.length > 0) {
        lines.push(chalk.bold('Options:'));
        for (const opt of options) {
          lines.push(`  ${chalk.green(helper.optionTerm(opt).padEnd(30))} ${chalk.dim(helper.optionDescription(opt))}`);
        }
        lines.push('');
      }

      return lines.join('\n');
    },
  });
  return cmd;
}

/**
 * Print a success message with a green checkmark.
 * In non-TTY mode, uses plain text prefix.
 */
export function success(msg: string): void {
  if (isTTY()) {
    out(`${chalk.green('✔')}  ${msg}`);
  } else {
    out(`SUCCESS: ${msg}`);
  }
}

/**
 * Print an error message with a red cross to stderr.
 * In non-TTY mode, uses plain text prefix.
 */
export function error(msg: string): void {
  if (isTTY()) {
    out(`${chalk.red('✖')}  ${chalk.red(msg)}`, 'error');
  } else {
    out(`ERROR: ${msg}`, 'error');
  }
}

/**
 * Print an info message with a blue info symbol.
 * In non-TTY mode, uses plain text prefix.
 */
export function info(msg: string): void {
  if (isTTY()) {
    out(`${chalk.blue('ℹ')}  ${msg}`);
  } else {
    out(`INFO: ${msg}`);
  }
}

/**
 * Print a warning message with a yellow warning symbol.
 * In non-TTY mode, uses plain text prefix.
 */
export function warn(msg: string): void {
  if (isTTY()) {
    out(`${chalk.yellow('⚠')}  ${chalk.yellow(msg)}`);
  } else {
    out(`WARN: ${msg}`);
  }
}

/**
 * Print a bold cyan section heading.
 */
export function heading(msg: string): void {
  out(`\n${chalk.bold.cyan(msg)}`);
}

/**
 * Print a formatted key-value label.
 */
export function label(key: string, value: string): void {
  out(`  ${chalk.bold(key + ':')} ${value}`);
}

/**
 * Create a no-op spinner for non-TTY environments.
 * Methods are safe to call but produce no ANSI output.
 */
function createNoOpSpinner(text: string): Spinner {
  out(text);
  const self: Spinner = {
    start: () => self,
    stop: () => self,
    success: (msg?: string) => { if (msg) out(`SUCCESS: ${msg}`); return self; },
    error: (msg?: string) => { if (msg) out(`ERROR: ${msg}`, 'error'); return self; },
    warning: (msg?: string) => { if (msg) out(`WARN: ${msg}`); return self; },
    info: (msg?: string) => { if (msg) out(`INFO: ${msg}`); return self; },
    clear: () => self,
    get text() { return text; },
    set text(value: string) { text = value; },
    get isSpinning() { return false; },
    get color() { return 'cyan' as const; },
    set color(_: string) { /* no-op */ },
  } as unknown as Spinner;
  return self;
}

/**
 * Start a spinner with the given text. Returns a wrapped spinner instance
 * whose success/error/warning/info methods add an extra space after the symbol
 * for consistent formatting.
 *
 * In non-TTY environments (CI pipelines, piped output), returns a no-op
 * spinner that outputs plain text without ANSI escape codes.
 */
export function startSpinner(text: string): Spinner {
  if (!isTTY()) {
    return createNoOpSpinner(text);
  }

  const spinner = yoctoSpinner({ text, color: 'cyan' }).start();

  const origSuccess = spinner.success.bind(spinner);
  const origError = spinner.error.bind(spinner);
  const origWarning = spinner.warning.bind(spinner);
  const origInfo = spinner.info.bind(spinner);

  spinner.success = (msg?: string) => origSuccess(msg ? ` ${msg}` : undefined);
  spinner.error = (msg?: string) => origError(msg ? ` ${msg}` : undefined);
  spinner.warning = (msg?: string) => origWarning(msg ? ` ${msg}` : undefined);
  spinner.info = (msg?: string) => origInfo(msg ? ` ${msg}` : undefined);

  return spinner;
}

/**
 * Format a status badge (enabled/disabled).
 */
export function badge(visible: boolean): string {
  return visible
    ? chalk.bgGreen.black('[visible]')
    : chalk.bgRed.white('[hidden]');
}

/**
 * Print a feature row with a styled badge and name.
 */
export function featureRow(name: string, visible: boolean): void {
  out(`    ${badge(visible)} ${name}`);
}

/**
 * Print a divider line.
 */
export function divider(): void {
  out(chalk.dim('─'.repeat(50)));
}

/**
 * Interactive yes/no confirmation on the terminal (default **No**). Only call on a TTY —
 * non-interactive callers must decide via their own flag (e.g. `login --yes`) instead.
 */
export async function confirm(question: string): Promise<boolean> {
  const readline = await import('node:readline/promises');
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  try {
    const answer = (await rl.question(`${question} [y/N] `)).trim().toLowerCase();
    return answer === 'y' || answer === 'yes';
  } finally {
    rl.close();
  }
}
