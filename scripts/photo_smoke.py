"""Exercise the real native PNG/mask loopback. Camera mode prints metrics only; never saves faces."""
import argparse
import json
import os
import socket
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from vision.photo import HEADER, MAX_PNG_BYTES


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--camera', action='store_true')
    args = parser.parse_args()
    import cv2
    import numpy as np
    log_path = ROOT / 'logs/photo-native-smoke.log'
    log_path.parent.mkdir(exist_ok=True)
    with log_path.open('w') as log:
        command = [sys.executable, '-m', 'vision', '--game-preview', '--no-preview', '--port', '0', '--preview-port', '0', '--photo-port', '0', '--ready-json', '--seconds', '14']
        if not args.camera:
            command.append('--demo')
        environment = dict(os.environ, MPLCONFIGDIR=str(ROOT / '.cache/matplotlib'))
        process = subprocess.Popen(command, cwd=ROOT, env=environment, stdout=subprocess.PIPE, stderr=log, text=True)
        try:
            ready = json.loads(process.stdout.readline())
            count = people = 0
            ages = []
            with socket.create_connection(('127.0.0.1', ready['photo_port']), timeout=30) as sock:
                stream = sock.makefile('rb')
                started = None
                while started is None or time.monotonic()-started < 5:
                    header = stream.read(HEADER.size)
                    if len(header) != HEADER.size:
                        raise RuntimeError('Photo service ended early')
                    magic, stamp, size, synthetic, present = HEADER.unpack(header)
                    assert magic == b'UGF1' and 33 <= size <= MAX_PNG_BYTES
                    assert synthetic == (not args.camera)
                    data = stream.read(size)
                    png = cv2.imdecode(np.frombuffer(data, np.uint8), cv2.IMREAD_UNCHANGED)
                    assert png.shape == (480, 640, 4)
                    assert np.all(png[png[:, :, 3] == 0, :3] == 0)
                    count += 1
                    people += bool(present)
                    if started is None:
                        started = time.monotonic()
                    ages.append(round(time.time()*1000-stamp))
                    if not args.camera and count == 1:
                        folder = ROOT / 'artifacts/photo-review'
                        folder.mkdir(parents=True, exist_ok=True)
                        (folder / 'synthetic-person.png').write_bytes(data)
            process.wait(timeout=15)
            print(json.dumps({'source': 'camera' if args.camera else 'synthetic', 'frames': count, 'person_frames': people, 'first_frame_age_ms': ages[0], 'latest_frame_age_ms': ages[-1], 'clean_rgba': True, 'real_images_saved': 0, 'exit_code': process.returncode}))
            assert process.returncode == 0 and count >= 8
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=5)


if __name__ == '__main__':
    main()
