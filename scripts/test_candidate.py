import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from validate_candidate import validate


class CandidateTests(unittest.TestCase):
    def test_tampered_missing_extra_wrong_source_and_dirty_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            data = b'MSI fixture (validator test, not installer test)'
            sha = hashlib.sha256(data).hexdigest()
            manifest = dict(product='OnlyCut', version='1.0.0', commit='a'*40, dirty=False,
                            productCode='code', assets=[dict(name='CutPic-Setup-x64.msi', size=len(data), sha256=sha)])
            metadata = dict(Properties=dict(ProductVersion='1.0.0', ProductName='CutPic', ProductCode='code',
                UpgradeCode='{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}', SecureCustomProperties='ONLYCUT_SAME_VERSION_FOUND'), Components=[['component']],
                Upgrades=[['{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}','1.0.0','1.0.0','770','ONLYCUT_SAME_VERSION_FOUND']],
                LaunchConditions=[['Installed OR NOT ONLYCUT_SAME_VERSION_FOUND','message']],
                ExecuteSequence=[['FindRelatedProducts','',25],['LaunchConditions','',100]],
                UISequence=[['FindRelatedProducts','',25],['LaunchConditions','',100]], SummaryTemplate='x64;2052')
            (root/'CutPic-Setup-x64.msi').write_bytes(data)
            (root/'msi-metadata.json').write_text(json.dumps(metadata))
            (root/'SHA256SUMS.txt').write_text(sha+'  CutPic-Setup-x64.msi')
            def write(value): (root/'candidate-manifest.json').write_text(json.dumps(value))
            write(manifest)
            self.assertEqual(validate(root, '1.0.0', 'a'*40), sha)
            for key, value in [('version','2.0.0'), ('commit','b'*40), ('dirty', True)]:
                with self.subTest(key=key):
                    bad=copy.deepcopy(manifest); bad[key]=value; write(bad)
                    with self.assertRaises(ValueError): validate(root, '1.0.0', 'a'*40)
            write(manifest)
            (root/'unexpected.txt').touch()
            with self.assertRaises(ValueError): validate(root, '1.0.0', 'a'*40)
            (root/'unexpected.txt').unlink()
            (root/'CutPic-Setup-x64.msi').write_bytes(b'tampered')
            with self.assertRaises(ValueError): validate(root, '1.0.0', 'a'*40)
            (root/'CutPic-Setup-x64.msi').unlink()
            with self.assertRaises(ValueError): validate(root, '1.0.0', 'a'*40)


if __name__ == '__main__': unittest.main()
