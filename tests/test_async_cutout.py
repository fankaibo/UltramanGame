import time
import unittest
from vision.async_cutout import AsyncPersonCutout


class TestSegmenter:
    def mask(self, image):
        if image == 'blocked':
            time.sleep(30)
        return 'mask-for-' + image
    def close(self): pass


class CutoutIsolationTests(unittest.TestCase):
    def take(self, service, timeout=6):
        until = time.monotonic()+timeout
        while time.monotonic()<until:
            value = service.take_latest()
            if value is not None: return value
            time.sleep(.01)
        self.fail('No cutout result')

    def test_image_and_mask_keep_the_same_capture_timestamp(self):
        service=AsyncPersonCutout(factory=TestSegmenter,startup_timeout=2)
        try:
            service.submit('frame-a',1000)
            self.assertEqual(('frame-a','mask-for-frame-a',1000),self.take(service))
            service.submit('frame-b',2000)
            self.assertEqual(('frame-b','mask-for-frame-b',2000),self.take(service))
            self.assertIsNone(service.take_latest())
        finally: service.close()
        self.assertFalse(service.thread.is_alive())

    def test_native_hang_does_not_block_camera_frames_and_recovers(self):
        service=AsyncPersonCutout(factory=TestSegmenter,startup_timeout=1,request_timeout=.25,retry_delay=.02)
        try:
            service.submit('warm',10);self.take(service)
            service.submit('blocked',20);time.sleep(.12)
            started=time.monotonic()
            for i in range(1000): service.submit('latest',30+i)
            self.assertLess(time.monotonic()-started,.5)
            result=self.take(service)
            self.assertEqual('latest',result[0])
            self.assertEqual('mask-for-latest',result[1])
            self.assertGreaterEqual(service.failures,1)
            self.assertGreaterEqual(service.generations,2)
        finally: service.close()
        self.assertFalse(service.thread.is_alive())

    def test_closing_blocked_native_call_releases_child(self):
        service=AsyncPersonCutout(factory=TestSegmenter,startup_timeout=3)
        service.submit('warm',1);self.take(service)
        service.submit('blocked',2);time.sleep(.1)
        process=service.process
        started=time.monotonic();service.close()
        self.assertLess(time.monotonic()-started,2.5)
        self.assertFalse(service.thread.is_alive())
        self.assertFalse(process.is_alive())
