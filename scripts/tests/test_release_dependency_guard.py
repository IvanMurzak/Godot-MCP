"""Cases for scripts/release_dependency_guard.py: which pins a release may ship (hermetic: a fake nuget.org)."""
import importlib.util
import json
import sys
from pathlib import Path
import tempfile
import textwrap
import unittest

_SCRIPT = Path(__file__).resolve().parents[1] / "release_dependency_guard.py"
_spec = importlib.util.spec_from_file_location("release_dependency_guard", _SCRIPT)
guard = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = guard  # dataclasses resolve annotations through sys.modules
_spec.loader.exec_module(guard)

PUBLISHED = {
    "com.ivanmurzak.reflectornet": ["5.4.0", "5.5.0"],
    "com.ivanmurzak.mcpplugin": ["8.13.1"],
    "com.ivanmurzak.rql": ["0.1.0", "0.2.0"],
    "com.ivanmurzak.codeexec": ["0.3.1"],
    "godotsharp": ["4.3.0"],
}


def fake_registry(published=PUBLISHED, fail=False):
    def fetch(url):
        if fail:
            raise guard.RegistryError("offline")
        package = url.split("/")[-2]
        versions = published.get(package)
        return None if versions is None else json.dumps({"versions": versions}).encode()
    return guard.NuGetRegistry(fetch)


def csproj(*references):
    lines = "\n".join(f'    <PackageReference Include="{i}" Version="{v}" />' for i, v in references)
    return f'<Project Sdk="Godot.NET.Sdk/4.3.0">\n  <ItemGroup>\n{lines}\n  </ItemGroup>\n</Project>\n'


ADDON_DEPS = """export const ADDON_PACKAGE_REFERENCES = [
  { id: 'com.IvanMurzak.ReflectorNet', version: '5.5.0' },
  { id: 'com.IvanMurzak.Rql', version: '%s', editorOnly: true },
];
"""


class Fixture:
    """A minimal Godot-MCP tree: the core csproj, one extension, the CLI pins."""

    def __init__(self, root: Path, rql="0.2.0", feed=False):
        self.root = root
        self.write("Godot-MCP.csproj", csproj(("com.IvanMurzak.ReflectorNet", "5.5.0"), ("com.IvanMurzak.Rql", rql)))
        self.write("extensions/Godot-AI-CSG/src/Godot-AI-CSG/Godot-AI-CSG.csproj",
                   csproj(("com.IvanMurzak.McpPlugin", "8.13.1"), ("com.IvanMurzak.Rql", rql), ("GodotSharp", "4.3.0")))
        # Test projects and testbeds ship nothing: their CI-only pins must not trip the guard.
        self.write("extensions/Godot-AI-CSG/testbed/CSG-Testbed.csproj",
                   csproj(("com.IvanMurzak.Godot.MCP.CSG", "0.0.0-ci")))
        self.write("extensions/Godot-AI-CSG/testbed/nuget.config",
                   '<configuration><packageSources><add key="local" value="../local-nuget" /></packageSources></configuration>')
        self.write("cli/src/utils/addon-deps.ts", ADDON_DEPS % rql)
        self.write("cli/package.json", json.dumps({"dependencies": {"@baizor/gamedev-cli-core": "^0.7.2"},
                                                   "devDependencies": {"vitest": "4.0.0-beta.1"}}, indent=2))
        if feed:
            self.write("Directory.Build.props", textwrap.dedent("""\
                <Project>
                  <PropertyGroup>
                    <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);$(MSBuildThisFileDirectory)prerelease-packages</RestoreAdditionalProjectSources>
                  </PropertyGroup>
                </Project>
                """))

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path


class GuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def problems(self, registry=None):
        _, problems = guard.run(self.root, registry or fake_registry())
        return [(guard.rel(p.file, self.root), p.message) for p in problems]

    def test_published_pins_pass(self):
        Fixture(self.root)
        pins, problems = guard.run(self.root, fake_registry())
        self.assertEqual([], problems)
        self.assertIn(("nuget", "GodotSharp", "4.3.0"), {(p.ecosystem, p.package, p.version) for p in pins})

    def test_prerelease_pins_and_feed_fail_everywhere_they_ship(self):
        Fixture(self.root, rql="0.3.0-dev.602ce7c", feed=True)
        problems = self.problems()
        files = {f for f, m in problems if "com.IvanMurzak.Rql 0.3.0-dev.602ce7c is a pre-release" in m}
        self.assertEqual({"Godot-MCP.csproj", "extensions/Godot-AI-CSG/src/Godot-AI-CSG/Godot-AI-CSG.csproj",
                          "cli/src/utils/addon-deps.ts"}, files)
        self.assertTrue(any(f == "Directory.Build.props" and "prerelease-packages" in m for f, m in problems))
        self.assertFalse(any("testbed" in f for f, _ in problems))

    def test_unpublished_stable_version_and_unknown_package_fail(self):
        Fixture(self.root, rql="0.9.0")
        problems = self.problems()
        self.assertIn(("Godot-MCP.csproj", "com.IvanMurzak.Rql 0.9.0 is not published on nuget.org"), problems)
        unknown = self.problems(fake_registry({**PUBLISHED, "com.ivanmurzak.rql": None}))
        self.assertIn(("Godot-MCP.csproj", "com.IvanMurzak.Rql is not a package on nuget.org"), unknown)

    def test_registry_outage_fails_closed(self):
        Fixture(self.root)
        problems = self.problems(fake_registry(fail=True))
        self.assertTrue(problems)
        self.assertTrue(all("cannot verify on nuget.org" in m for _, m in problems))

    def test_msbuild_property_versions_are_resolved(self):
        Fixture(self.root)
        self.root.joinpath("Godot-MCP.csproj").write_text(textwrap.dedent("""\
            <Project Sdk="Godot.NET.Sdk/4.3.0">
              <PropertyGroup><RqlVersion>$(RqlBase)-dev.1</RqlVersion><RqlBase>0.3.0</RqlBase></PropertyGroup>
              <PropertyGroup Condition="'$(Configuration)' == 'Debug'"><RqlVersion>0.2.0</RqlVersion></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="com.IvanMurzak.Rql" Version="$(RqlVersion)" />
                <PackageReference Include="com.IvanMurzak.ReflectorNet"><Version>$(Missing)</Version></PackageReference>
              </ItemGroup>
            </Project>
            """), encoding="utf-8")
        problems = self.problems()
        # A property set under different Conditions: every value it may take is checked.
        self.assertIn(("Godot-MCP.csproj", "com.IvanMurzak.Rql 0.3.0-dev.1 is a pre-release version"), problems)
        self.assertIn(("Godot-MCP.csproj", "com.IvanMurzak.ReflectorNet: cannot resolve version '$(Missing)'"), problems)
        # write-nuget-deps.mjs copies the csproj text: a `$(Prop)` version would ship literally.
        self.assertIn(("Godot-MCP.csproj", "com.IvanMurzak.Rql: nuget-deps.json would ship the literal version "
                                           "'$(RqlVersion)' (write-nuget-deps.mjs does not evaluate MSBuild)"), problems)

    def test_commented_core_reference_ships_through_nuget_deps(self):
        Fixture(self.root)
        csproj = self.root / "Godot-MCP.csproj"
        csproj.write_text(csproj.read_text(encoding="utf-8").replace(
            "</Project>", '  <!-- <PackageReference Include="com.IvanMurzak.Rql" Version="0.3.0-dev.9" /> -->\n</Project>'),
            encoding="utf-8")
        self.assertEqual([("Godot-MCP.csproj", "com.IvanMurzak.Rql 0.3.0-dev.9 is a pre-release version")], self.problems())

    def test_extension_json_project_outside_src_is_scanned(self):
        fixture = Fixture(self.root)
        fixture.write("extensions/Godot-AI-X/extension.json", json.dumps({"project": "extensions/Godot-AI-X/X.csproj"}))
        fixture.write("extensions/Godot-AI-X/X.csproj", csproj(("com.IvanMurzak.Rql", "0.3.0-dev.3")))
        self.assertEqual([("extensions/Godot-AI-X/X.csproj", "com.IvanMurzak.Rql 0.3.0-dev.3 is a pre-release version")],
                         self.problems())
        fixture.write("extensions/Godot-AI-Y/extension.json", json.dumps({"project": "extensions/Godot-AI-Y/missing.csproj"}))
        self.assertIn(("extensions/Godot-AI-Y/missing.csproj",
                       "expected file is missing (update scripts/release_dependency_guard.py)"), self.problems())

    def test_version_override_and_package_download(self):
        fixture = Fixture(self.root)
        fixture.write("extensions/Godot-AI-CSG/src/Godot-AI-CSG/Godot-AI-CSG.csproj", textwrap.dedent("""\
            <Project Sdk="Godot.NET.Sdk/4.3.0">
              <ItemGroup>
                <PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.13.1" VersionOverride="8.14.0-rc.1" />
                <PackageDownload Include="com.IvanMurzak.ReflectorNet" Version="[5.6.0-beta]" />
              </ItemGroup>
            </Project>
            """))
        problems = self.problems()
        self.assertIn(("extensions/Godot-AI-CSG/src/Godot-AI-CSG/Godot-AI-CSG.csproj",
                       "com.IvanMurzak.McpPlugin 8.14.0-rc.1 is a pre-release version"), problems)
        self.assertIn(("extensions/Godot-AI-CSG/src/Godot-AI-CSG/Godot-AI-CSG.csproj",
                       "com.IvanMurzak.ReflectorNet 5.6.0-beta is a pre-release version"), problems)

    def test_central_package_versions_and_nuget_config_feed(self):
        fixture = Fixture(self.root)
        fixture.write("Directory.Packages.props",
                      '<Project><ItemGroup><PackageVersion Include="com.IvanMurzak.Rql" Version="[0.3.0-dev.2]" />'
                      '</ItemGroup></Project>')
        fixture.write("nuget.config", '<configuration><packageSources><clear />'
                      '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
                      '<add key="pre" value="prerelease-packages" /></packageSources></configuration>')
        problems = self.problems()
        self.assertIn(("Directory.Packages.props", "com.IvanMurzak.Rql 0.3.0-dev.2 is a pre-release version"), problems)
        self.assertIn(("nuget.config", "package source 'pre' is the local feed 'prerelease-packages'"), problems)
        self.assertFalse(any("nuget.org'" in m for _, m in problems))

    def test_npm_prerelease_and_local_specs_fail(self):
        fixture = Fixture(self.root)
        fixture.write("cli/package.json", json.dumps(
            {"dependencies": {"@baizor/gamedev-cli-core": "^0.8.0-dev.1", "local": "file:../core",
                              "tagged": "next", "gh": "owner/repo#main", "ok": ">=1.2.0 <2"}}))
        problems = self.problems()
        self.assertIn(("cli/package.json", "@baizor/gamedev-cli-core ^0.8.0-dev.1 is a pre-release version"), problems)
        self.assertIn(("cli/package.json", "local: 'file:../core' is not a registry version"), problems)
        self.assertIn(("cli/package.json", "tagged: 'next' is not a registry version"), problems)
        self.assertIn(("cli/package.json", "gh: 'owner/repo#main' is not a registry version"), problems)
        self.assertEqual(4, len(problems))

    def test_missing_shipped_file_fails(self):
        Fixture(self.root)
        self.root.joinpath("cli/src/utils/addon-deps.ts").unlink()
        self.assertIn(("cli/src/utils/addon-deps.ts",
                       "expected file is missing (update scripts/release_dependency_guard.py)"), self.problems())


class VersionTests(unittest.TestCase):
    def test_prerelease(self):
        self.assertTrue(guard.is_prerelease("0.3.0-dev.602ce7c"))
        self.assertTrue(guard.is_prerelease("1.0.0-rc.1+build"))
        self.assertFalse(guard.is_prerelease("1.0.0+build-7"))
        self.assertFalse(guard.is_prerelease("8.13.1"))

    def test_normalize(self):
        self.assertEqual("1.0.0", guard.normalize_nuget_version("1.0"))
        self.assertEqual("1.0.0", guard.normalize_nuget_version("1.0.0.0"))
        self.assertEqual("1.2.3.4", guard.normalize_nuget_version("01.2.3.4"))
        self.assertEqual("1.0.0-rc.1", guard.normalize_nuget_version("1.0.0-RC.1+meta"))

    def test_ranges(self):
        self.assertEqual((["1.0.0"], None), guard.nuget_versions_in("[1.0.0]"))
        self.assertEqual((["1.0"], None), guard.nuget_versions_in("[1.0,2.0)"))  # exclusive upper bound need not exist
        self.assertEqual((["1.0", "2.0"], None), guard.nuget_versions_in("[1.0,2.0]"))
        self.assertEqual((["1.0"], None), guard.nuget_versions_in("[1.0, )"))
        self.assertEqual(([], "floating version '1.*' is not a pin"), guard.nuget_versions_in("1.*"))


if __name__ == "__main__":
    unittest.main()
