"""Run the built player through a complete round using development input playback."""
import json
import plistlib
import re
import subprocess
import time
import argparse
from pathlib import Path


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--width',type=int,default=1920);parser.add_argument('--height',type=int,default=1080)
    args=parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    app = root / 'unity/Builds/TigaTraining.app'
    with (app / 'Contents/Info.plist').open('rb') as stream:
        binary = app / 'Contents/MacOS' / plistlib.load(stream)['CFBundleExecutable']
    log = root / 'logs/cinematic-player.log'
    start = time.monotonic()
    with (root / 'logs/cinematic-player-console.log').open('w') as console:
        player = subprocess.Popen([str(binary), '--keyboard', '--review-playback', '-screen-fullscreen', '0',
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
    fps = [float(value) for value in re.findall(r'renderFps=(\d+\.\d+)', output)]
    result = {'result': 'passed', 'camera_used': False, 'wall_seconds': round(time.monotonic()-start, 2),
              'summary': match[0], 'fps_windows': fps,'requested_resolution':[args.width,args.height]}
    (root / 'artifacts/cinematic-combat/player-validation.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
