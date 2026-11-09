/**
 * CI gate: does this pull request change only documentation?
 *
 *   node scripts/ci/docs-only.mjs
 *
 * Writes `docs_only=true|false` to $GITHUB_OUTPUT; test_pull_request.yml skips its heavy jobs only
 * on an explicit `true`. So every doubt means a full run: a path not listed below, an empty diff,
 * or a failure here, since this step is `continue-on-error` and leaves the output empty. HEAD must
 * be GITHUB_SHA, so a failed checkout cannot classify some other commit.
 *
 * Reads the diff of the merge commit GitHub builds for a pull_request run (HEAD^1 is the base),
 * which needs `fetch-depth: 2` on the checkout and no token or API. `--no-renames` because a rename
 * from addons/ into docs/ would otherwise list only its docs/ side.
 *
 * To skip CI for more paths, edit DOCS and its table in docs-only.test.mjs: a new doc runs everything
 * until someone checks it and lists it here. List only what no build, test, packaging or generation
 * step reads. These do not qualify:
 *   README.md, docs/ARCHITECTURE.md, docs/RELEASING.md  cli/tests/skills-addon-parity.test.ts asserts on them
 *   docs/assetlib/                                      the same test, and scripts/bump-version.mjs rewrites it
 *   READMEs and the changelog in cli/, extensions/      packed into the npm and NuGet packages
 *   anything but pictures under docs/img/               Godot-MCP.csproj has no Compile Remove for docs/: a .cs there builds
 */

import { execFileSync } from "node:child_process";
import { appendFileSync, realpathSync } from "node:fs";
import { fileURLToPath } from "node:url";

const DOCS = new Set(["AGENTS.md", "docs/EXTENSION-MONOREPO.md", "docs/rql.md", "docs/runtime-security.md"]);

/** @param {string} path repo-relative, forward slashes */
export function isDocsPath(path) {
  return DOCS.has(path) || /^docs\/img\/.+\.(png|jpg|svg)$/.test(path) || /^\.(claude|agents)\//.test(path);
}

/** @param {readonly string[]} files */
export function isDocsOnly(files) {
  return files.length > 0 && files.every(isDocsPath);
}

/** The files the pull request's merge commit changes against its base. */
export function changedFiles(repo) {
  const git = (...args) => execFileSync("git", args, { cwd: repo, encoding: "utf8" });
  git("rev-parse", "--verify", "HEAD^2"); // throws unless HEAD is the merge commit
  return git("diff", "--name-only", "--no-renames", "-z", "HEAD^1", "HEAD").split("\0").filter(Boolean);
}

function main() {
  const head = execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim();
  if (head !== process.env.GITHUB_SHA) throw new Error(`HEAD ${head} is not GITHUB_SHA ${process.env.GITHUB_SHA}`);
  const files = changedFiles(process.cwd());
  const docsOnly = isDocsOnly(files);
  const others = files.filter((file) => !isDocsPath(file));
  console.log(`${files.length} changed file(s), ${others.length} outside the docs list${others.length ? `, e.g. ${others[0]}` : ""}`);
  console.log(`docs_only=${docsOnly}`);
  appendFileSync(process.env.GITHUB_OUTPUT, `docs_only=${docsOnly}\n`);
}

// realpath: import.meta.url has its symlinks resolved, argv[1] does not, and a mismatch would exit 0 with no output.
if (process.argv[1] && realpathSync(process.argv[1]) === fileURLToPath(import.meta.url)) main();
