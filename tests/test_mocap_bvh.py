"""Numeric BVH conversion checks; no downloaded motion or Unity installation needed."""
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path
import numpy as np

SCRIPTS = Path(__file__).resolve().parents[1] / 'scripts'

def module(name):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / (name + '.py'))
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value

reader = module('inspect_mocap_bvh')
preparer = module('prepare_recovery_motion')

class MocapBvhTests(unittest.TestCase):
    def source(self, text):
        directory = tempfile.TemporaryDirectory();self.addCleanup(directory.cleanup)
        path = Path(directory.name) / '140_03.bvh';path.write_text(text)
        return path

    def test_nested_offsets_follow_ordered_rotations(self):
        path = self.source('''HIERARCHY
ROOT Hips {
 OFFSET 0 0 0
 CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation
 JOINT Limb { OFFSET 0 1 0 CHANNELS 1 Zrotation End Site { OFFSET 0 1 0 } }
}
MOTION
Frames: 1
Frame Time: 0.01
10 20 30 90 90 0 90
''')
        joints, positions, rotations, dt = reader.read_bvh(path)
        self.assertEqual([j['parent'] for j in joints], [-1,0,1])
        self.assertEqual(dt, .01)
        np.testing.assert_allclose(positions[0], [[10,20,30],[10,20,31],[10,19,31]], atol=1e-10)
        np.testing.assert_allclose(rotations[0,0] @ rotations[0,0].T, np.eye(3), atol=1e-10)

    def fixture(self):
        legs = []
        for side,x in [('Left',1),('Right',-1)]:
            legs.append(f'''JOINT {side}UpLeg {{ OFFSET {x} 0 0 CHANNELS 0
 JOINT {side}Leg {{ OFFSET 0 -2 0 CHANNELS 0
  JOINT {side}Foot {{ OFFSET 0 -2 0 CHANNELS 0
   JOINT {side}ToeBase {{ OFFSET 0 0 1 CHANNELS 0 End Site {{ OFFSET 0 0 1 }} }}
  }}
 }}
}}''')
        return 'HIERARCHY\nROOT Hips { OFFSET 0 0 0 CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation\n'+'\n'.join(legs)+'\n}\nMOTION\nFrames: 5\nFrame Time: 0.0083333\n'+'\n'.join(f'{f} 5 2 0 0 0' for f in range(5))+'\n'

    def test_anatomical_reflection_and_nonuniform_last_sample_time(self):
        path = self.source(self.fixture())
        data = preparer.prepare(path,1,4,2)
        self.assertEqual(data['sourceFrames'], [1,3,4])
        self.assertAlmostEqual(data['times'][-1], data['duration'])
        self.assertLess(data['times'][2]-data['times'][1],data['times'][1]-data['times'][0])
        self.assertAlmostEqual(data['legLength'],4)
        points = np.array(data['frames'][-1]['positions']).reshape(-1,3)
        self.assertLess(points[data['joints'].index('LeftUpLeg'),0],0)
        self.assertGreater(points[data['joints'].index('RightUpLeg'),0],0)
        np.testing.assert_allclose(points[0],[0,5,0])
        self.assertEqual(len(data['sourceSha256']),64)

    def test_invalid_ranges_reject_calibration_and_out_of_bounds(self):
        path = self.source(self.fixture())
        for first,last,stride in [(0,4,1),(1,5,1),(4,1,1),(1,4,0)]:
            with self.subTest(first=first,last=last,stride=stride), self.assertRaises(ValueError):
                preparer.prepare(path,first,last,stride)

    def test_corrupt_motion_is_rejected(self):
        for text in [self.fixture().replace('Frame Time: 0.0083333','Frame Time: 0'),
                     self.fixture().replace('Frames: 5','Frames: 6'),
                     self.fixture().replace('0 5 2 0 0 0','nan 5 2 0 0 0')]:
            with self.subTest(text=text[-80:]), self.assertRaises(ValueError):
                reader.read_bvh(self.source(text))

if __name__ == '__main__': unittest.main()
