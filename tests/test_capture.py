import threading
import unittest

from vision.capture import LatestCapture


class CaptureTests(unittest.TestCase):
    def test_slow_consumer_gets_latest_frame_without_queue(self):
        published = threading.Event()
        released = threading.Event()
        class Camera:
            count = 0
            def isOpened(self): return True
            def read(self):
                self.count += 1
                if self.count <= 100: return True, self.count
                published.set()
                return False, None
            def release(self): released.set()
        with LatestCapture(Camera) as capture:
            self.assertTrue(published.wait(2))
            sample = capture.take()
            self.assertEqual((100, 100), sample[:2])
            self.assertIsNone(capture.take(after=100, timeout=.01))
        self.assertTrue(released.is_set())

    def test_camera_open_failure_reaches_consumer_and_releases_camera(self):
        released = []
        class Camera:
            def isOpened(self): return False
            def release(self): released.append(True)
        with LatestCapture(Camera) as capture:
            with self.assertRaises(RuntimeError): capture.take(timeout=2)
        self.assertEqual([True], released)
