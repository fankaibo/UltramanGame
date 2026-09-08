"""Small, mirrored, annotated images for the game; memory and loopback only."""
import math
import struct
import time

from .bridge import LatestBridge

HEADER = struct.Struct("!4sqIBB")
MAX_JPEG_BYTES = 128 * 1024
MAX_WIDTH, MAX_HEIGHT = 320, 240


def encode_preview(jpeg, captured_ms, source, quality):
    if not 4 <= len(jpeg) <= MAX_JPEG_BYTES or not jpeg.startswith(b"\xff\xd8") or not jpeg.endswith(b"\xff\xd9"):
        raise ValueError("Invalid or oversized preview JPEG")
    if source not in ("camera", "synthetic") or quality not in (0, 1, 2) or captured_ms < 0:
        raise ValueError("Invalid preview metadata")
    return HEADER.pack(b"UGP1", captured_ms, len(jpeg), source == "synthetic", quality) + jpeg


def visible(point, confidence):
    return (all(math.isfinite(v) for v in (point.x, point.y, point.visibility))
            and 0 <= point.x <= 1 and 0 <= point.y <= 1 and confidence <= point.visibility <= 1)


def quality_of(points):
    if not points or len(points) != 33 or not all(visible(points[i], .45) for i in (11, 12)):
        return 0
    return 2 if all(visible(points[i], .55) for i in range(11, 17)) else 1


class GamePreview:
    def __init__(self, port=8766):
        self.bridge = LatestBridge(port)
        self.next_at = 0
        self.frames = 0

    def publish(self, image, points, captured_ms, source="camera"):
        now = time.monotonic()
        if now < self.next_at:
            return
        self.next_at = now + .1  # At most 10 FPS; full-rate poses use a separate connection.
        import cv2
        if image is None:
            # Explicit synthetic test image. No camera is opened in demo mode.
            import numpy as np
            image = np.full((240, 320, 3), (38, 28, 20), dtype=np.uint8)
        h, w = image.shape[:2]
        scale = min(MAX_WIDTH/w, MAX_HEIGHT/h)
        preview = cv2.resize(image, (max(1, round(w*scale)), max(1, round(h*scale))))
        h, w = preview.shape[:2]
        if points and len(points) == 33:
            for a, b in ((11, 12), (11, 13), (13, 15), (12, 14), (14, 16)):
                if visible(points[a], .55) and visible(points[b], .55):
                    cv2.line(preview, (round(points[a].x*(w-1)), round(points[a].y*(h-1))),
                             (round(points[b].x*(w-1)), round(points[b].y*(h-1))), (240, 220, 70), 2, cv2.LINE_AA)
            for i in range(11, 17):
                p = points[i]
                if visible(p, 0):
                    color = (100, 240, 90) if visible(p, .55) else (40, 175, 255)
                    cv2.circle(preview, (round(p.x*(w-1)), round(p.y*(h-1))), 4, color, -1, cv2.LINE_AA)
        preview = cv2.flip(preview, 1)
        if source == "synthetic":
            cv2.putText(preview, "SYNTHETIC TEST", (12, 225), cv2.FONT_HERSHEY_SIMPLEX, .5, (255, 255, 255), 1)
        ok, data = cv2.imencode(".jpg", preview, [cv2.IMWRITE_JPEG_QUALITY, 75])
        if ok:
            self.bridge.publish(encode_preview(data.tobytes(), captured_ms, source, quality_of(points)))
            self.frames += 1

    def __enter__(self):
        self.bridge.__enter__()
        return self

    def __exit__(self, *args):
        self.bridge.__exit__(*args)
