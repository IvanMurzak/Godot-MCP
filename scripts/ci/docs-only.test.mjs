// Run: node --test scripts/ci/docs-only.test.mjs

import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";
import { changedFiles, isDocsOnly, isDocsPath } from "./docs-only.mjs";

test("isDocsPath lists only what nothing reads", () => {
  const skip = [
    "AGENTS.md",
    "docs/rql.md",
    "docs/runtime-security.md",
    "docs/EXTENSION-MONOREPO.md",
    "docs/img/mcp-clients/claude-64.png",
    "docs/img/promo/ai-developer-banner.jpg",
    "docs/img/promo/hazzard-features.svg",
  ];
  const run = [
    // Read by cli/tests/skills-addon-parity.test.ts.
    "README.md",
    "docs/ARCHITECTURE.md",
    "docs/RELEASING.md",
    "docs/assetlib/SUBMISSION.md",
    // The rest of docs/assetlib/, and what nobody has checked yet.
    "docs/assetlib/gallery/01-hero.jpg",
    "docs/new.md",
    "docs/plans/a.md",
    // Godot-MCP.csproj's SDK glob compiles a .cs anywhere under docs/.
    "docs/a.cs",
    "docs/img/a.cs",
    "docs/img/a.md",
    // Packed into a package, or read by the build and the tests.
    "cli/AGENTS.md",
    "cli/README.md",
    "cli/docs/README.md",
    "cli/CHANGELOG.md",
    "extensions/Godot-AI-CSG/README.md",
    "addons/godot_mcp/extensions.catalog.md",
    "Godot-Tests/README.md",
    "glama/README.md",
    "templates/extension/README.md",
    // Code, and near misses of the list.
    "addons/godot_mcp/plugin.cfg",
    "Godot-MCP.csproj",
    ".github/workflows/test_pull_request.yml",
    ".github/assetlib/edit.hbs",
    "scripts/ci/docs-only.mjs",
    "docs",
    "documents/a.md",
    "agents.md",
    "docs/rql.txt",
  ];
  for (const path of skip) assert.equal(isDocsPath(path), true, path);
  for (const path of run) assert.equal(isDocsPath(path), false, path);
});

test("no test, script or workflow names a path the list skips", () => {
  const repo = fileURLToPath(new URL("../..", import.meta.url));
  const out = execFileSync(
    "git",
    ["grep", "-hoE", `['"](docs/[^'"]+|AGENTS\\.md)['"]`, "--", "cli/tests", "cli/src", "Godot-MCP.Tests", "Godot-Tests", "scripts", ".github", ":!scripts/ci"],
    { cwd: repo, encoding: "utf8" },
  );
  const named = [...new Set(out.split("\n").filter(Boolean).map((literal) => literal.slice(1, -1)))];
  assert.ok(named.includes("docs/RELEASING.md"), "the scan must find the docs the parity test reads");
  assert.deepEqual(named.filter(isDocsPath), []);
});

test("isDocsOnly needs at least one file and every file on the list", () => {
  assert.equal(isDocsOnly(["docs/rql.md", "AGENTS.md"]), true);
  assert.equal(isDocsOnly(["docs/rql.md", "README.md"]), false);
  assert.equal(isDocsOnly([]), false);
});

function withRepo(check) {
  const repo = mkdtempSync(join(tmpdir(), "docs-only-"));
  const git = (...args) => execFileSync("git", ["-c", "user.name=t", "-c", "user.email=t@t", ...args], { cwd: repo, stdio: "pipe" });
  try {
    git("init", "-b", "main");
    check(repo, git);
  } finally {
    rmSync(repo, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 }); // Windows briefly keeps git's handles
  }
}

/** A repo with one base commit, then the merge commit of a branch `change` applied to it. */
function withMerge(change, check) {
  withRepo((repo, git) => {
    const write = (path) => {
      mkdirSync(dirname(join(repo, path)), { recursive: true });
      writeFileSync(join(repo, path), path);
    };
    write("addons/a.cs");
    write("addons/b.cs"); // keeps addons/ alive, or git reads a rename of a.cs as moving the folder
    git("add", ".");
    git("commit", "-m", "base");
    git("checkout", "-b", "pr");
    change({ repo, write });
    git("add", ".");
    git("commit", "-m", "pr");
    git("checkout", "main");
    write("addons/moved-on-main.cs");
    git("add", ".");
    git("commit", "-m", "main moved");
    git("merge", "--no-ff", "-m", "merge", "pr");
    check(repo);
  });
}

test("changedFiles lists only what the pull request changes, not what main gained", () => {
  withMerge(({ write }) => write("docs/rql.md"), (repo) => assert.deepEqual(changedFiles(repo), ["docs/rql.md"]));
});

test("a rename from code into docs lists both sides, so it is not docs-only", () => {
  withMerge(
    ({ repo }) => {
      mkdirSync(join(repo, "docs"));
      renameSync(join(repo, "addons/a.cs"), join(repo, "docs/rql.md"));
    },
    (repo) => {
      assert.deepEqual(changedFiles(repo).sort(), ["addons/a.cs", "docs/rql.md"]);
      assert.equal(isDocsOnly(changedFiles(repo)), false);
    },
  );
});

test("a non-ASCII path stays unquoted, so it can match the list", () => {
  withMerge(({ write }) => write("docs/img/заметка.png"), (repo) => assert.equal(isDocsOnly(changedFiles(repo)), true));
});

test("changedFiles refuses a HEAD that is not a merge commit", () => {
  withRepo((repo, git) => {
    git("commit", "--allow-empty", "-m", "only");
    assert.throws(() => changedFiles(repo));
  });
});

test("the script writes the output only when HEAD is the commit under test", () => {
  const script = fileURLToPath(new URL("./docs-only.mjs", import.meta.url));
  withMerge(({ write }) => write("docs/rql.md"), (repo) => {
    const output = join(repo, "output.txt");
    const run = (sha) => spawnSync(process.execPath, [script], { cwd: repo, encoding: "utf8", env: { ...process.env, GITHUB_OUTPUT: output, GITHUB_SHA: sha } });

    assert.notEqual(run("0".repeat(40)).status, 0);
    assert.throws(() => readFileSync(output));

    const head = execFileSync("git", ["rev-parse", "HEAD"], { cwd: repo, encoding: "utf8" }).trim();
    assert.equal(run(head).status, 0);
    assert.equal(readFileSync(output, "utf8"), "docs_only=true\n");
  });
});
