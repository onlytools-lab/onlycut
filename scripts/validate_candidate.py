"""Check file/JSON consistency only. On Windows use verify-msi-candidate.ps1
to independently reopen the actual MSI. Adjacent JSON is not trusted metadata.
"""
import argparse
import hashlib
import json
from check_installer_policy import check_metadata
from pathlib import Path


def validate(directory, version, commit, allow_dirty=False):
    root = Path(directory)
    manifest = json.loads((root / 'candidate-manifest.json').read_text(encoding='utf-8-sig'))
    metadata = json.loads((root / 'msi-metadata.json').read_text(encoding='utf-8-sig'))
    expected = {'CutPic-Setup-x64.msi', 'candidate-manifest.json', 'msi-metadata.json', 'SHA256SUMS.txt'}
    if {p.name for p in root.iterdir()} != expected:
        raise ValueError('Missing or unexpected candidate files')
    if manifest['version'] != version or manifest['commit'] != commit or manifest['product'] != 'OnlyCut':
        raise ValueError('Wrong candidate version/source/product')
    if manifest['dirty'] and not allow_dirty:
        raise ValueError('Dirty source is not a final candidate')
    if len(manifest['assets']) != 1:
        raise ValueError('Expected exactly one MSI asset')
    asset = manifest['assets'][0]
    if asset['name'] != 'CutPic-Setup-x64.msi':
        raise ValueError('Unexpected asset name')
    data = (root / asset['name']).read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if len(data) != asset['size'] or digest != asset['sha256']:
        raise ValueError('MSI size/hash mismatch')
    if (root / 'SHA256SUMS.txt').read_text().strip() != f"{digest}  {asset['name']}":
        raise ValueError('Checksum file mismatch')
    props = metadata['Properties']
    if (props['ProductVersion'] != version or props['ProductName'] != 'CutPic'
            or props['UpgradeCode'].upper() != '{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}'
            or props['ProductCode'] != manifest['productCode'] or not metadata['Components']
            or not metadata['Upgrades'] or not metadata['SummaryTemplate'].startswith('x64;')):
        raise ValueError('MSI metadata mismatch')
    check_metadata(metadata, version)
    return digest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('directory')
    parser.add_argument('--version', required=True)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--allow-dirty', action='store_true')
    args = parser.parse_args()
    print(validate(args.directory, args.version, args.commit, args.allow_dirty))
