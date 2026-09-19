"""Run the built player through a complete round using development input playback."""
import json
import plistlib
import re
import subprocess
import time
import argparse
import hashlib
from datetime import datetime, timezone
from pathlib import Path


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--width',type=int,default=1920);parser.add_argument('--height',type=int,default=1080)
    args=parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    started = datetime.now(timezone.utc)
    evidence = root / 'artifacts/cinematic-combat/player' / started.strftime('%Y%m%dT%H%M%S%fZ')
    native = evidence / 'native'
    native.mkdir(parents=True)
    app = root / 'unity/Builds/TigaTraining.app'
    with (app / 'Contents/Info.plist').open('rb') as stream:
        binary = app / 'Contents/MacOS' / plistlib.load(stream)['CFBundleExecutable']
    log = root / 'logs/cinematic-player.log'
    assembly = app / 'Contents/Resources/Data/Managed/Assembly-CSharp.dll'
    assembly_sha = hashlib.sha256(assembly.read_bytes()).hexdigest()
    start = time.monotonic()
    with (root / 'logs/cinematic-player-console.log').open('w') as console:
        player = subprocess.Popen([str(binary), '--keyboard', '--review-playback', '--guided-proof',
                                   '--proof-output', str(native), '-screen-fullscreen', '0',
                                   '-screen-width', str(args.width), '-screen-height', str(args.height), '-logFile', str(log)],
                                  cwd=root, stdout=console, stderr=subprocess.STDOUT)
        try:
            code = player.wait(timeout=180)
        finally:
            if player.poll() is None:
                player.terminate()
                try:
                    player.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    player.kill()
                    player.wait()
    output = log.read_text(errors='replace')
    (evidence / 'player.log').write_text(output)
    match = re.search(r'\[FullGameReview\] pass=True[^\n]+', output)
    if code != 0 or not match:
        raise RuntimeError(f'Full player review failed, exit={code}; inspect {log}')
    if re.search(r'(?:NullReferenceException|ArgumentException|Shader error|error CS\d)', output):
        raise RuntimeError(f'Runtime exception in {log}')
    if '[PresentationWarmup] complete' not in output:
        raise RuntimeError('Presentation was not prewarmed')
    if output.count('[Voice] key=beam_original playing=True') != 2:
        raise RuntimeError('Expected two original battle cries')
    if 'reaction=3.0' not in output:
        raise RuntimeError('Missing child reaction-time evidence')
    # These images come from this player run, not the independent Editor render
    # in cinematic-combat/frames. A unique directory prevents stale visual proof.
    required = ('battle-entry', 'monster-rush-left', 'monster-rush-right', 'guard-impact', 'hero-hurt',
                'beam-closeup-peak', 'beam-firing', 'Paused', 'Victory')
    for name in required:
        path = native / (name + '.png')
        if not path.is_file() or f'file={path}' not in output:
            raise RuntimeError(f'Missing current-player visual evidence: {name}')
    fps = [float(value) for value in re.findall(r'renderFps=(\d+\.\d+)', output)]
    result = {'result': 'passed', 'camera_used': False, 'wall_seconds': round(time.monotonic()-start, 2),
              'summary': match[0], 'fps_windows': fps,'requested_resolution':[args.width,args.height],
              'started_utc': started.isoformat(), 'assembly_sha256': assembly_sha,
              'evidence_directory': str(evidence),
              'screenshots': sorted(path.name for path in native.glob('*.png'))}
    (evidence / 'validation.json').write_text(json.dumps(result, indent=2))
    (root / 'artifacts/cinematic-combat/player-validation.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
