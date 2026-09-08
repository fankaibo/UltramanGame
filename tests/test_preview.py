import socket
import unittest

from vision.bridge import LatestBridge
from vision.demo import landmarks_at
from vision.preview import HEADER, MAX_JPEG_BYTES, encode_preview, quality_of


class PreviewTests(unittest.TestCase):
    def test_matched_frame_metadata_and_bounded_packet(self):
        jpeg = b"\xff\xd8sample\xff\xd9"
        packet = encode_preview(jpeg, 123456789, "synthetic", 1)
        self.assertEqual((b"UGP1", 123456789, len(jpeg), 1, 1), HEADER.unpack(packet[:HEADER.size]))
        self.assertEqual(jpeg, packet[HEADER.size:])
        for invalid in (b"not jpeg", b"\xff\xd8" + b"x" * MAX_JPEG_BYTES + b"\xff\xd9"):
            with self.assertRaises(ValueError):
                encode_preview(invalid, 123, "camera", 2)
        for source, quality in (("unknown", 2), ("camera", 3)):
            with self.assertRaises(ValueError):
                encode_preview(jpeg, 123, source, quality)

    def test_status_distinguishes_occlusion_missing_body_and_small_player(self):
        p = landmarks_at(0)
        self.assertEqual(2, quality_of(p))
        p[12].x = p[11].x - .02
        self.assertEqual(2, quality_of(p))
        p[13].visibility = .2
        self.assertEqual(1, quality_of(p))
        p[12].visibility = .44
        self.assertEqual(0, quality_of(p))
        self.assertEqual(0, quality_of(None))

    def test_preview_is_latest_only_loopback_and_releases_its_port(self):
        with LatestBridge(0) as bridge:
            for stamp in range(100):
                bridge.publish(encode_preview(b"\xff\xd8x\xff\xd9", stamp, "camera", 2))
            address = bridge.address
            with socket.create_connection(address, timeout=2) as sock:
                header = sock.makefile('rb').read(HEADER.size)
                self.assertEqual(99, HEADER.unpack(header)[1])
                self.assertEqual('127.0.0.1', address[0])
        with LatestBridge(address[1]) as restarted:
            self.assertEqual(address, restarted.address)
