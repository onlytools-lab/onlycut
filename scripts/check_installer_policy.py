"""Check compatibility-sensitive WiX source and actual MSI policy tables."""
import xml.etree.ElementTree as ET

UPGRADE = '6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A'
PROPERTY = 'ONLYCUT_SAME_VERSION_FOUND'
CONDITION = 'Installed OR NOT ONLYCUT_SAME_VERSION_FOUND'


def check_source(path='installer/Package.wxs'):
    ns = {'w': 'http://wixtoolset.org/schemas/v4/wxs'}
    p = ET.parse(path).getroot().find('w:Package', ns)
    if p.get('UpgradeCode') != UPGRADE or p.get('Name') != 'CutPic' or p.get('Scope') != 'perMachine':
        raise ValueError('Installer identity/scope changed')
    major = p.find('w:MajorUpgrade', ns)
    if major is None or major.get('AllowSameVersionUpgrades', 'no') != 'no':
        raise ValueError('MajorUpgrade same-version replacement must remain disabled')
    detector = p.find(f"w:Upgrade[@Id='{UPGRADE}']/w:UpgradeVersion[@Property='{PROPERTY}']", ns)
    expected = dict(Minimum='$(var.ProductVersion)', Maximum='$(var.ProductVersion)', IncludeMinimum='yes', IncludeMaximum='yes', OnlyDetect='yes')
    if detector is None or any(detector.get(k) != v for k, v in expected.items()):
        raise ValueError('Missing exact-version detect-only guard')
    if not any(e.get('Condition') == CONDITION for e in p.findall('w:Launch', ns)):
        raise ValueError('Missing maintenance-safe launch guard')
    for id_, name in [('INSTALLFOLDER','CutPic'), ('ProgramMenuCutPicFolder','CutPic')]:
        if p.find(f".//w:Directory[@Id='{id_}']", ns).get('Name') != name:
            raise ValueError('Compatibility directory changed')


def check_metadata(metadata, version):
    rows = [r for r in metadata['Upgrades'] if r[4] == PROPERTY]
    if len(rows) != 1:
        raise ValueError('Expected one equal-version detector')
    code, low, high, bits, prop = rows[0]
    if code.strip('{}').upper() != UPGRADE or low != version or high != version or int(bits) != (2 | 256 | 512):
        raise ValueError('Detector must be exact-version, detect-only, both bounds inclusive')
    if not any(r[0] == CONDITION for r in metadata['LaunchConditions']):
        raise ValueError('Missing actual maintenance-safe launch condition')
    for key in ['ExecuteSequence', 'UISequence']:
        seq = {r[0]: int(r[2]) for r in metadata[key]}
        if not 0 < seq['FindRelatedProducts'] < seq['LaunchConditions']:
            raise ValueError(f'{key}: detection must precede launch conditions')
    if PROPERTY not in metadata['Properties'].get('SecureCustomProperties', '').split(';'):
        raise ValueError('Detector property must be secure')
