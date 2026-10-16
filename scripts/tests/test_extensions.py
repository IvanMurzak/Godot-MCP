"""Regression guards for source-only releases and isolated test execution."""
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import extension_live
import extension_release
import extensions
import new_extension


class PackageVerification(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / 'module'
        self.source.mkdir()
        self.module = {'project': 'module/package.csproj', 'packageId': 'example', 'version': '1.2.3'}
        (self.source / 'package.csproj').write_text('''<Project><ItemGroup>
<PackageReference Include="com.IvanMurzak.McpPlugin" Version="8.7.0"/>
<PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.4.1"/>
</ItemGroup></Project>''')
        self.files = {'src/Runtime/Test.cs': b'class Example {}', 'build/example.props': b'<Project/>',
                      'README.md': b'Package readme'}
        for name, value in self.files.items():
            path = self.source / name.removeprefix('src/')
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(value)
        self.files['example.nuspec'] = b'''<package><metadata><id>example</id><version>1.2.3</version>
<dependencies><dependency id="com.IvanMurzak.McpPlugin" version="8.7.0"/>
<dependency id="com.IvanMurzak.ReflectorNet" version="5.4.1"/></dependencies></metadata></package>'''

    def verify(self):
        archive = self.root / 'example.nupkg'
        with zipfile.ZipFile(archive, 'w') as bundle:
            for name, value in self.files.items():
                bundle.writestr(name, value)
        return extensions.verify_package(archive, self.module, self.root)

    def test_repository_signature_is_allowed(self):
        self.files['.signature.p7s'] = b'registry signature'
        self.assertTrue(self.verify())

    def test_modified_source_is_rejected(self):
        self.files['src/Runtime/Test.cs'] += b'// modified'
        with self.assertRaisesRegex(ValueError, 'bytes differ'):
            self.verify()

    def test_sibling_source_is_rejected(self):
        self.files['src/Runtime/Sibling.cs'] = b'class Sibling {}'
        with self.assertRaisesRegex(ValueError, 'unexpected sources'):
            self.verify()

    def test_compiled_godot_dll_is_rejected(self):
        self.files['lib/net8.0/GodotSharp.dll'] = b'dll'
        with self.assertRaisesRegex(ValueError, 'DLL'):
            self.verify()

    def test_wrong_package_identity_is_rejected(self):
        self.files['example.nuspec'] = self.files['example.nuspec'].replace(b'<id>example', b'<id>sibling')
        with self.assertRaisesRegex(ValueError, 'identity/version'):
            self.verify()

    def test_changed_dependency_floor_is_rejected(self):
        self.files['example.nuspec'] = self.files['example.nuspec'].replace(b'8.7.0', b'9.0.0')
        with self.assertRaisesRegex(ValueError, 'dependency floor'):
            self.verify()

    def test_train_package_requires_exact_candidate_dependency(self):
        (self.root / 'Directory.Build.targets').write_text('''<Project><ItemGroup>
<PackageReference Update="com.IvanMurzak.McpPlugin" Version="[8.7.1-ws.ga1b2c3d4]"/>
</ItemGroup></Project>''')
        with patch.dict(os.environ, {'CHAIN_LOCK': 'candidate lock'}):
            with self.assertRaisesRegex(ValueError, 'dependency floor'):
                self.verify()
            self.files['example.nuspec'] = self.files['example.nuspec'].replace(b'8.7.0', b'[8.7.1-ws.ga1b2c3d4]')
            self.assertTrue(self.verify())


class SuiteGuards(unittest.TestCase):
    def test_script_failure_is_never_hidden_by_gridmap_diagnostic(self):
        text = ('ERROR: Parameter "instance" is null.\ninstance_geometry_set_transparency\n'
                'Tool_GridMap\nSCRIPT ERROR: Missing autoload')
        with self.assertRaisesRegex(ValueError, 'Script/assembly'):
            extension_live.check_editor_log(text)

    def test_unrelated_native_error_is_fatal(self):
        with self.assertRaisesRegex(ValueError, 'Unexpected engine error'):
            extension_live.check_editor_log('ERROR: Parameter "instance" is null.\nAnotherTool')

    def test_observed_headless_diagnostic_is_counted(self):
        self.assertEqual(1, extension_live.check_editor_log(
            'ERROR: Parameter "instance" is null.\ninstance_geometry_set_transparency\nTool_GridMap')['headlessGridMapGizmo'])

    def test_godot44_native_cold_import_is_reported_only_during_startup(self):
        error = ('ERROR: Do not use progress dialog (task) while flushing the message queue or using call_deferred()!\n'
                 '   at: add_task (editor/progress_dialog.cpp:183)\n')
        boot = '[Godot-MCP] plugin loaded\n'
        self.assertEqual(1, extension_live.check_editor_log(error + boot, '4.4.0', startup_lines=3)['godot44ColdImportProgress'])
        with self.assertRaisesRegex(ValueError, 'Unexpected'):
            extension_live.check_editor_log(error + boot, '4.5.1', startup_lines=3)
        with self.assertRaisesRegex(ValueError, 'Unexpected'):
            extension_live.check_editor_log(boot + 'running tools\n' * 20 + error, '4.4.0', startup_lines=1)

    def test_archive_traversal_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with zipfile.ZipFile(root / 'bad.zip', 'w') as bundle:
                bundle.writestr('../escaped.txt', b'bad')
            with self.assertRaisesRegex(ValueError, 'escapes'):
                extension_live.extract(root / 'bad.zip', root / 'target')
            self.assertFalse((root / 'escaped.txt').exists())

    def test_release_reuses_published_package_without_building(self):
        module = {'name': 'Example', 'packageId': 'example', 'version': '1.2.3'}
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            existing = root / 'registry.nupkg'
            existing.write_bytes(b'existing immutable bytes')
            with patch.object(extension_release, 'published', return_value=True), \
                    patch.object(extension_release, 'verify_registry', return_value=existing), \
                    patch.object(extension_release, 'pack') as build, \
                    patch.dict(os.environ, {'GITHUB_OUTPUT': str(root / 'outputs')}):
                extension_release.stage([module], root)
            build.assert_not_called()
            self.assertEqual([], json.loads((root / 'pending.json').read_text()))
            self.assertEqual(b'existing immutable bytes', (root / 'example.1.2.3.nupkg').read_bytes())

    def test_scaffold_registers_module_without_legacy_workflows(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for folder in ('extensions', 'templates/extension', 'addons/godot_mcp', 'cli/src/utils'):
                shutil.copytree(extensions.ROOT / folder, root / folder,
                                ignore=shutil.ignore_patterns('bin', 'obj', '.godot', '__pycache__'))
            folder = new_extension.create('ExampleFeature', root)
            self.assertFalse((folder / '.github').exists())
            self.assertEqual('Godot-AI-ExampleFeature', extensions.modules(root)[-1]['name'])
            self.assertNotIn('YOUR_FEATURE', (folder / 'extension.json').read_text())
            with self.assertRaisesRegex(ValueError, 'already exists'):
                new_extension.create('ExampleFeature', root)


if __name__ == '__main__':
    unittest.main()
