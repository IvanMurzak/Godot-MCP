"""AssetLib archive isolation, API reconciliation and secret-safe failures."""
import io
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import zipfile

original_path = sys.path[:]
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import assetlib_extensions as assetlib
spec = importlib.util.spec_from_file_location('assetlib_package_fixture', Path(__file__).with_name('test_extensions.py'))
fixtures = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixtures)
sys.path[:] = original_path


class ArchiveDistribution(unittest.TestCase):
    def setUp(self):
        self.package_fixture = fixtures.PackageVerification()
        self.package_fixture.setUp()
        self.addCleanup(self.package_fixture.doCleanups)
        self.root = self.package_fixture.root
        self.module = self.package_fixture.module

    def fixture(self):
        self.package_fixture.verify()
        (self.root / 'LICENSE').write_text('Apache-2.0 fixture\n')
        module = {**self.module, 'name': 'Godot-AI-Example', 'minGodotVersion': '4.3.0'}
        package = self.root / 'example.nupkg'
        archive = assetlib.pack_archive(module, package, self.root / 'output', root=self.root)
        return module, package, archive

    def test_archive_contains_only_selected_sources_and_installs_under_addons(self):
        module, package, archive = self.fixture()
        with zipfile.ZipFile(archive) as bundle:
            self.assertEqual(5, len(bundle.namelist()))
            self.assertTrue(all('/addons/godot_mcp_extensions/Example/' in name for name in bundle.namelist()))
        target = self.root / 'consumer'
        assetlib.extract_archive(archive, target)
        self.assertEqual(b'class Example {}', (target / 'addons/godot_mcp_extensions/Example/Runtime/Test.cs').read_bytes())
        self.assertFalse(list(target.rglob('*.props')))
        self.assertFalse(list(target.rglob('*.csproj')))
        assetlib.verify_archive(module, package, archive, root=self.root)

    def test_archive_is_deterministic_and_foreign_sources_are_rejected(self):
        module, package, archive = self.fixture()
        before = archive.read_bytes()
        assetlib.pack_archive(module, package, archive.parent, root=self.root)
        self.assertEqual(before, archive.read_bytes())
        with zipfile.ZipFile(archive, 'a') as bundle:
            bundle.writestr('other/addons/Sibling/Tool.cs', 'class Sibling {}')
        with self.assertRaisesRegex(ValueError, 'unexpected files'):
            assetlib.verify_archive(module, package, archive, root=self.root)

    def test_archive_modified_source_is_rejected(self):
        module, package, archive = self.fixture()
        with zipfile.ZipFile(archive) as bundle:
            files = {name: bundle.read(name) for name in bundle.namelist()}
        name = next(name for name in files if name.endswith('.cs'))
        files[name] += b'// changed'
        with zipfile.ZipFile(archive, 'w') as bundle:
            for name, data in files.items():
                bundle.writestr(name, data)
        with self.assertRaisesRegex(ValueError, 'bytes differ'):
            assetlib.verify_archive(module, package, archive, root=self.root)

    def test_extraction_rejects_traversal(self):
        archive = self.root / 'traversal.zip'
        with zipfile.ZipFile(archive, 'w') as bundle:
            bundle.writestr('release/../../outside/file.cs', b'bad')
        with self.assertRaisesRegex(ValueError, 'Invalid'):
            assetlib.extract_archive(archive, self.root / 'project')


class FakeApi:
    def __init__(self, owned=(), pending=(), details=None):
        self.owned, self.pending, self.details = list(owned), list(pending), details or {}
        self.posts = []

    def listing(self, path, **query):
        return self.owned if path == '/asset' else self.pending

    def request(self, path, body=None):
        if body is None:
            return self.details[path]
        self.posts.append((path, body))
        return {'id': 42}


class Reconciliation(unittest.TestCase):
    def setUp(self):
        self.module = {'name': 'Godot-AI-Example', 'packageId': 'com.IvanMurzak.Godot.MCP.Example',
                       'version': '1.2.3', 'minGodotVersion': '4.4.0'}
        self.title = assetlib.payload(self.module)['title']

    def test_initial_submission_is_reported_as_pending_not_public(self):
        api = FakeApi()
        result = assetlib.submit(self.module, api, 'owner', 'fixture-token')
        self.assertEqual({'status': 'submitted', 'editId': 42}, result)
        self.assertEqual('/asset', api.posts[0][0])
        self.assertEqual('Custom', api.posts[0][1]['download_provider'])
        self.assertEqual(assetlib.archive_url(self.module), api.posts[0][1]['download_commit'])
        self.assertEqual('4.4', api.posts[0][1]['godot_version'])

    def test_retry_reuses_matching_pending_edit_without_post(self):
        api = FakeApi(pending=[{'title': self.title, 'author': 'owner', 'edit_id': 7}],
                      details={'/asset/edit/7': {'version_string': '1.2.3',
                               'download_commit': assetlib.archive_url(self.module), 'status': 'in_review'}})
        self.assertEqual({'status': 'submitted', 'editId': 7}, assetlib.submit(self.module, api, 'owner', 'fixture-token'))
        self.assertEqual([], api.posts)

    def test_approved_asset_is_reused_and_wrong_archive_fails(self):
        asset = {'title': self.title, 'author': 'owner', 'asset_id': 9, 'version_string': '1.2.3'}
        api = FakeApi(owned=[asset], details={'/asset/9': {'download_commit': assetlib.archive_url(self.module)}})
        self.assertEqual({'status': 'published', 'assetId': 9}, assetlib.submit(self.module, api, 'owner', 'fixture-token'))
        self.assertEqual([], api.posts)
        api.details['/asset/9']['download_commit'] = 'https://example.invalid/wrong.zip'
        with self.assertRaisesRegex(ValueError, 'different archive'):
            assetlib.submit(self.module, api, 'owner', 'fixture-token')

    def test_in_review_old_submission_is_not_duplicated(self):
        api = FakeApi(pending=[{'title': self.title, 'author': 'owner', 'edit_id': 7}],
                      details={'/asset/edit/7': {'version_string': '1.0.0', 'status': 'in_review'}})
        with self.assertRaisesRegex(ValueError, 'in review'):
            assetlib.submit(self.module, api, 'owner', 'fixture-token')
        self.assertEqual([], api.posts)

    def test_editable_pending_submission_updates_same_id(self):
        api = FakeApi(pending=[{'title': self.title, 'author': 'owner', 'edit_id': 7}],
                      details={'/asset/edit/7': {'version_string': '1.0.0', 'status': 'new'}})
        assetlib.submit(self.module, api, 'owner', 'fixture-token')
        self.assertEqual('/asset/edit/7', api.posts[0][0])

    def test_ambiguous_entries_fail_without_mutating(self):
        item = {'title': self.title, 'author': 'owner', 'edit_id': 7}
        api = FakeApi(pending=[item, {**item, 'edit_id': 8}])
        with self.assertRaisesRegex(ValueError, 'Ambiguous'):
            assetlib.submit(self.module, api, 'owner', 'fixture-token')
        self.assertEqual([], api.posts)

    def test_newer_public_version_is_never_downgraded(self):
        api = FakeApi(owned=[{'title': self.title, 'author': 'owner', 'asset_id': 9, 'version_string': '2.0.0'}])
        with self.assertRaisesRegex(ValueError, 'Newer'):
            assetlib.submit(self.module, api, 'owner', 'fixture-token')
        self.assertEqual([], api.posts)

    def test_http_failure_does_not_expose_response_body_or_credentials(self):
        error = urllib.error.HTTPError(assetlib.API + '/login', 403, 'Forbidden', {}, io.BytesIO(b'fixture-secret'))
        with patch.object(assetlib.urllib.request, 'urlopen', side_effect=error):
            with self.assertRaisesRegex(RuntimeError, 'HTTP 403') as caught:
                assetlib.Api().request('/login', {'password': 'fixture-password'})
        self.assertNotIn('fixture-secret', str(caught.exception))
        self.assertNotIn('fixture-password', str(caught.exception))


if __name__ == '__main__':
    unittest.main()
