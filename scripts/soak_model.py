"""Exercise fresh native image allocations without opening or recording a camera."""
import argparse
import json
from pathlib import Path
import resource
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seconds", type=float, default=720)
    parser.add_argument("--fps", type=float, default=30)
    parser.add_argument("--cpu", action="store_true", help="Compare CPU in an isolated dependency environment")
    args = parser.parse_args()
    if args.seconds <= 0 or args.fps <= 0:
        parser.error("seconds and fps must be positive")
    import numpy as np
    from vision import model as setup
    if args.cpu:
        setup.USE_METAL = False
    frames = poses = 0
    inference = 0
    start = time.monotonic()
    next_log = 0
    image = np.zeros((480, 640, 3), dtype=np.uint8)
    def report(complete=False):
        elapsed = time.monotonic() - start
        # macOS reports ru_maxrss in bytes; Linux reports KiB. This is a high
        # watermark, not current memory usage or by itself evidence of a leak.
        unit = 1048576 if sys.platform == "darwin" else 1024
        print(json.dumps(dict(test="fresh-generated-frames", complete=complete,
            mediapipe=setup.mp.__version__, backend="Metal" if setup.USE_METAL else "CPU",
            seconds=round(elapsed, 1), frames=frames, poses=poses,
            mean_inference_ms=round(inference * 1000 / max(frames, 1), 2),
            peak_rss_mib=round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss / unit, 1))), flush=True)
    with setup.create_landmarker(ROOT / "models/pose_landmarker_lite.task") as model:
        while time.monotonic() - start < args.seconds:
            tick = time.monotonic()
            # Construct a new Image per camera frame. Reusing one native image
            # (as the quick smoke test does) would miss pixel-buffer exhaustion.
            result = model.detect_for_video(setup.model_image(image), round(frames * 1000 / args.fps))
            inference += time.monotonic() - tick
            frames += 1
            poses += bool(result.pose_landmarks)
            elapsed = time.monotonic() - start
            if elapsed >= next_log:
                report()
                next_log += 10
            time.sleep(max(0, frames / args.fps - elapsed))
    report(complete=True)


if __name__ == "__main__":
    main()
