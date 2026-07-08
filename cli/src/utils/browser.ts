import { execFile } from 'child_process';
import { verbose } from './ui.js';

/**
 * Open a URL in the user's default browser.
 * Silently ignores errors — the URL is always shown in the terminal as a fallback.
 */
export function openBrowser(url: string): void {
  // Only open well-formed http(s) URLs. The URL comes from a device-auth server
  // (which `--base-url` can point at an arbitrary host), so it is untrusted input;
  // refusing any non-http(s) scheme keeps a hostile/MITM'd `verification_uri` from
  // being handed to the platform opener (notably `cmd /c start` on Windows).
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    verbose(`Refusing to open malformed URL: ${url}`);
    return;
  }
  if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
    verbose(`Refusing to open non-http(s) URL: ${url}`);
    return;
  }

  const platform = process.platform;
  let cmd: string;
  let args: string[];

  if (platform === 'darwin') {
    cmd = 'open';
    args = [url];
  } else if (platform === 'win32') {
    cmd = 'cmd';
    args = ['/c', 'start', '', url];
  } else {
    cmd = 'xdg-open';
    args = [url];
  }

  verbose(`Opening browser: ${cmd} ${args.join(' ')}`);
  execFile(cmd, args, (err) => {
    if (err) {
      verbose(`Failed to open browser: ${err.message}`);
    }
  });
}
