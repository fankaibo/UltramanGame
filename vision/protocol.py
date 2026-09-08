"""Versioned, bounded pose messages. Images are never sent or recorded."""
import json
import math
import time
import uuid

SCHEMA = 1
POINT_COUNT = 33
MAX_LINE_BYTES = 32768


class FrameFactory:
    def __init__(self, source="camera"):
        if source not in ("camera", "synthetic"):
            raise ValueError("Unknown pose source")
        self.source = source
        self.stream_id = uuid.uuid4().hex
        self.sequence = 0

    def make(self, landmarks=None, captured_ms=None):
        self.sequence += 1
        points = []
        if landmarks is not None and len(landmarks) == POINT_COUNT:
            for p in landmarks:
                values = [float(getattr(p, key)) for key in ("x", "y", "z", "visibility")]
                if not all(math.isfinite(value) for value in values):
                    points = []
                    break
                points.append(dict(zip(("x", "y", "z", "visibility"), values)))
        return {
            "schema": SCHEMA,
            "source": self.source,
            "streamId": self.stream_id,
            "sequence": self.sequence,
            "capturedMs": int(time.time() * 1000) if captured_ms is None else int(captured_ms),
            "tracked": len(points) == POINT_COUNT,
            "points": points,
        }


def encode(frame):
    line = (json.dumps(frame, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    if len(line) > MAX_LINE_BYTES:
        raise ValueError("Pose frame exceeds protocol size limit")
    return line
