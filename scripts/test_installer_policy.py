import copy
import unittest
from check_installer_policy import check_source, check_metadata


class InstallerPolicyTests(unittest.TestCase):
    def test_compatibility_source(self):
        check_source()

    def test_actual_policy_positive_and_negative(self):
        metadata = dict(Upgrades=[['{6E02C1AA-9A9D-4CB1-8F6F-37AD338F316A}','1.0.1','1.0.1','770','ONLYCUT_SAME_VERSION_FOUND']],
                        LaunchConditions=[['Installed OR NOT ONLYCUT_SAME_VERSION_FOUND','reason']],
                        Properties={'SecureCustomProperties':'ONLYCUT_SAME_VERSION_FOUND'},
                        ExecuteSequence=[['FindRelatedProducts','',25],['LaunchConditions','',100]],
                        UISequence=[['FindRelatedProducts','',25],['LaunchConditions','',100]])
        check_metadata(metadata, '1.0.1')
        for attribute in ['1','2','258','512']:
            bad = copy.deepcopy(metadata); bad['Upgrades'][0][3] = attribute
            with self.subTest(bits=attribute), self.assertRaises(ValueError): check_metadata(bad, '1.0.1')
        for key in ['ExecuteSequence','UISequence']:
            bad = copy.deepcopy(metadata); bad[key][0][2]=200
            with self.subTest(sequence=key), self.assertRaises(ValueError): check_metadata(bad, '1.0.1')
        bad = copy.deepcopy(metadata); bad['LaunchConditions'][0][0]='NOT ONLYCUT_SAME_VERSION_FOUND'
        with self.assertRaises(ValueError): check_metadata(bad, '1.0.1')


if __name__ == '__main__': unittest.main()
