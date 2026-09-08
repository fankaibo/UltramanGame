"""On-demand clean person cutouts. No files, annotations, second camera or uploads."""
import struct
import time
import math

from .bridge import LatestBridge, _Handler

HEADER = struct.Struct("!4sqIBB")
MAX_PNG_BYTES = 2 * 1024 * 1024


def encode_photo(png, captured_ms, synthetic, present):
    if not 33 <= len(png) <= MAX_PNG_BYTES or png[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Invalid photo PNG")
    width, height = struct.unpack("!II", png[16:24])
    if png[12:16] != b"IHDR" or not 0 < width <= 640 or not 0 < height <= 480 or png[24:26] != b"\x08\x06":
        raise ValueError("Photo must be bounded 8-bit RGBA")
    if captured_ms <= 0 or type(synthetic) is not bool or type(present) is not bool:
        raise ValueError("Invalid photo metadata")
    return HEADER.pack(b"UGF1", captured_ms, len(png), synthetic, present) + png


class _PhotoHandler(_Handler):
    def handle(self):
        bridge = self.server.bridge
        with bridge.changed:
            bridge.subscribers += 1
        try:
            super().handle()
        finally:
            with bridge.changed:
                bridge.subscribers -= 1
                if not bridge.subscribers:
                    bridge.latest = b""


class GamePhoto:
    def __init__(self, port=8767):
        self.bridge = LatestBridge(port, _PhotoHandler)
        self.bridge.subscribers = 0
        self.next_at = 0

    def due(self):
        return self.bridge.subscribers > 0 and time.monotonic() >= self.next_at

    def publish(self, image, mask, captured_ms, synthetic=False):
        if not self.due():
            return
        self.next_at = time.monotonic() + .125
        import cv2
        import numpy as np
        if synthetic:
            # Procedural test stand-in, deliberately recognisable as a drawing, never a real face.
            image = np.zeros((480, 640, 3), np.uint8)
            cv2.circle(image, (320, 116), 58, (145, 195, 240), -1)
            cv2.rectangle(image, (245, 176), (395, 345), (220, 145, 40), -1)
            wave = math.sin(captured_ms / 600) * 60
            for a, b in (((255, 192), (180, round(130+wave))), ((385, 192), (450, round(130-wave))), ((280, 325), (260, 465)), ((360, 325), (380, 465))):
                cv2.line(image, a, b, (220, 145, 40), 40)
            cv2.circle(image, (299, 108), 7, (30, 40, 45), -1)
            cv2.circle(image, (341, 108), 7, (30, 40, 45), -1)
            cv2.ellipse(image, (320, 128), (21, 12), 0, 0, 180, (30, 40, 45), 3)
            cv2.putText(image, "TEST", (271, 274), cv2.FONT_HERSHEY_SIMPLEX, 1.2, (255, 255, 255), 3)
            image = cv2.flip(image, 1)  # The later camera mirror should leave this test label readable.
            mask = (image.max(axis=2) > 0).astype(np.float32)
        rgba = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
        alpha = np.zeros(image.shape[:2], np.uint8)
        present = False
        if mask is not None:
            mask = np.nan_to_num(np.asarray(mask), nan=0.0, posinf=0.0, neginf=0.0)
            mask = cv2.resize(mask, (image.shape[1], image.shape[0]))
            alpha = (np.clip((mask - .25) / .5, 0, 1) * 255).astype(np.uint8)
            present = np.count_nonzero(alpha > 128) >= image.shape[0] * image.shape[1] * .015
        rgba[:, :, 3] = alpha if present else 0
        # RGB at zero alpha is erased too; background pixels never enter the photo stream.
        rgba[rgba[:, :, 3] == 0, :3] = 0
        rgba = cv2.flip(rgba, 1)
        ok, png = cv2.imencode(".png", rgba, [cv2.IMWRITE_PNG_COMPRESSION, 2])
        if ok:
            self.bridge.publish(encode_photo(png.tobytes(), captured_ms, synthetic, bool(present)))

    def __enter__(self):
        self.bridge.__enter__()
        return self

    def __exit__(self, *args):
        self.bridge.__exit__(*args)
