"""Verify high-hand alternating cast continuity against the actual A183 source."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a184-volley-continuity'
BASELINE = '6d1a68b5b4a92666d7ec043011ad547922627f64'


def read(path):
    with path.open() as stream:
        return list(csv.DictReader(stream))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    for name in ('Runtime/RiggedActor.cs', 'Core/HeroPunchLink.cs'):
        old = (FOLDER / ('before-' + Path(name).name)).read_bytes()
        assert old == subprocess.check_output(['git', 'show', BASELINE + ':unity/Assets/Scripts/' + name], cwd=ROOT)
        for version in ('before', 'after'):
            data = old if version == 'before' else (ROOT / 'unity/Assets/Scripts' / name).read_bytes()
            assert name + ' ' + hashlib.sha256(data).hexdigest() in (FOLDER / version / 'sources.txt').read_text()
    rows = 0
    measurements = []
    for hz in (15, 30, 60):
        for speed in ('0.75', '1.70'):
            key = f'{hz}-{speed}'
            assert (FOLDER / f'before/{key}-rules.csv').read_bytes() == (FOLDER / f'after/{key}-rules.csv').read_bytes()
            before, after = (read(FOLDER / f'{v}/{key}-trace.csv') for v in ('before', 'after'))
            assert len(before) == len(after) == hz * 6
            rows += len(after)
            peaks = []
            for trace in (before, after):
                prior = 0
                handoffs = []
                for r in trace:
                    sequence = int(r['sequence'])
                    if sequence > prior and prior > 0:
                        handoffs.append(float(r['rightStep' if r['action'] == 'LeftPunch' else 'leftStep']))
                    prior = sequence
                assert len(handoffs) == 5
                peaks.append(max(handoffs))
            active_error = 0
            for a, b in zip(before, after):
                assert a['feetStep'] == b['feetStep'] and a['headStep'] == b['headStep']
                if a['action'] in ('LeftPunch', 'RightPunch'):
                    side = 'left' if a['action'] == 'LeftPunch' else 'right'
                    active_error = max(active_error, math.sqrt(sum((float(a[side + v]) - float(b[side + v])) ** 2 for v in 'XYZ')))
            assert active_error < .00001
            assert peaks[1] < peaks[0] * .85, (key, peaks)
            measurements.append(dict(hz=hz, speed=float(speed), support_handoff_before=peaks[0], support_handoff_after=peaks[1], reduction=1-peaks[1]/peaks[0], active_hand_difference=active_error))
    transitions = (FOLDER / 'transitions/transitions.txt').read_text().splitlines()
    assert len(transitions) == 15 and all('rangeToMeleeGuard=pass' in row for row in transitions)
    movie = FOLDER / 'continuous-before-after.mp4'
    if not args.verify_only:
        for version in ('before', 'after'):
            assert len(list((FOLDER / version / 'frames').glob('*.jpg'))) == 360
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/frames/%04d.jpg'),
                        '-framerate', '30', '-i', str(FOLDER / 'after/frames/%04d.jpg'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                        '-map', '[v]', '-frames:v', '360', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    media = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(movie)], text=True))['streams'][0]
    assert int(media['nb_frames']) == 360 and media['width'] == 2560 and media['height'] == 720
    (FOLDER / 'comparison.json').write_text(json.dumps(dict(baseline=BASELINE, identical_state_rows=rows, measurements=measurements, transition_groups=15, frames=360, seconds=12,
        note='Left baseline / right candidate; accepted alternating casts with high synthetic hand pose. Metrics measure the supporting wrist at handoff, not overall animation quality or recognition accuracy.'), indent=2)+'\n')
    print(f'PASS {rows} identical states, unchanged firing wrist and head/foot frame displacement, improved support handoffs; 15 transition groups')


if __name__ == '__main__':
    main()
