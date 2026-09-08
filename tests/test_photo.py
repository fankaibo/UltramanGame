import socket
import struct
import time
import unittest

from vision.photo import GamePhoto, HEADER, MAX_PNG_BYTES, encode_photo


class PhotoTests(unittest.TestCase):
    def png(self):
        return b'\x89PNG\r\n\x1a\n' + struct.pack('!I4sII', 13, b'IHDR', 640, 480) + b'\x08\x06' + bytes(7)

    def test_photo_metadata_and_limits(self):
        packet = encode_photo(self.png(), 123456, True, True)
        self.assertEqual((b'UGF1', 123456, 33, 1, 1), HEADER.unpack(packet[:18]))
        for png in (b'bad', self.png() + bytes(MAX_PNG_BYTES), self.png()[:16] + struct.pack('!I', 641) + self.png()[20:]):
            with self.assertRaises(ValueError):
                encode_photo(png, 123456, False, True)
        with self.assertRaises(ValueError):
            encode_photo(self.png(), 0, False, True)

    def test_segmentation_only_when_photo_subscribed_and_released_after_exit(self):
        with GamePhoto(0) as photo:
            self.assertFalse(photo.due())
            with socket.create_connection(photo.bridge.address, timeout=2) as sock:
                deadline = time.monotonic() + 2
                while not photo.due() and time.monotonic() < deadline:
                    time.sleep(.01)
                self.assertTrue(photo.due())
                photo.bridge.publish(encode_photo(self.png(), 123456, True, True))
                self.assertEqual(b'UGF1', sock.recv(51)[:4])
                sock.shutdown(socket.SHUT_RDWR)
            deadline = time.monotonic() + 2
            while photo.bridge.subscribers and time.monotonic() < deadline:
                photo.bridge.publish(encode_photo(self.png(), 123456, True, True))
                time.sleep(.02)
            self.assertEqual(0, photo.bridge.subscribers)
            self.assertEqual(b'', photo.bridge.latest)
