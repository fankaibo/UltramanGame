"""Encode actual Unity review frames and event-aligned local game audio."""
import argparse
import csv
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--baseline', type=Path, help='300-frame original RiggedReview output')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root / 'artifacts/cinematic-combat'
    frames = sorted((folder / 'frames').glob('frame-*.png'))
    if not (folder / 'validation.txt').exists():
        parser.error('a passed Unity full-battle render is required')
    for i, frame in enumerate(frames):
        if frame.name != f'frame-{i:04d}.png':
            parser.error(f'non-contiguous frames at {i}')
    duration = len(frames) / 30
    sounds = []
    audio = root / 'unity/Assets/Resources/Audio'
    voice = root / 'unity/Assets/Resources/Voice'
    events = list(csv.DictReader((folder / 'events.csv').open()))
    for e in events:
        at = float(e['seconds'])
        name = e['event']
        effect = {'Transform': 'transform', 'Punch': 'swing', 'EnemyAttack': 'enemy_rush',
                  'Block': 'shield', 'Hurt': 'impact', 'Resume': 'recover', 'Victory': 'victory'}.get(name)
        if effect:
            sounds.append((audio / f'{effect}.wav', at, .62))
        if name == 'Punch':
            sounds.append((audio / 'impact.wav', at + .12, .58))
        if name == 'Beam':
            original = voice / 'beam_original.aiff'
            if original.exists():
                sounds.append((original, at, .9))
            sounds.append((audio / 'beam.wav', at + .98 + .28, .5))
    # This review uses the same game music/effects. Guided dialogue is exercised in
    # the real player test; it is not synthesized or falsely synchronized here.
    cmd = [args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '30',
           '-i', str(folder / 'frames/frame-%04d.png'), '-stream_loop', '-1', '-i', str(audio / 'music_battle.wav')]
    filters = [f'[1:a]volume=.18,atrim=0:{duration}[music]']
    mix = ['[music]']
    for i, (path, at, volume) in enumerate(sounds, 2):
        cmd += ['-i', str(path)]
        filters.append(f'[{i}:a]volume={volume},adelay={round(at*1000)}:all=1[a{i}]')
        mix.append(f'[a{i}]')
    filters.append(''.join(mix) + f'amix=inputs={len(mix)}:duration=longest:normalize=0,alimiter=limit=.93,atrim=0:{duration}[mix]')
    graph = folder / 'audio-filter.txt'
    graph.write_text(';\n'.join(filters))
    output = folder / 'full-battle.mp4'
    cmd += ['-filter_complex_script', str(graph), '-map', '0:v', '-map', '[mix]', '-t', str(duration),
            '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '192k',
            '-movflags', '+faststart', str(output)]
    subprocess.run(cmd, check=True)
    first_block = next(float(e['seconds']) for e in events if e['event'] == 'Block')
    first_beam = next(float(e['seconds']) for e in events if e['event'] == 'Beam')
    # A compact highlight preserves real-time motion: counter/block, two punches,
    # then the complete charge, release and recovery of the first special move.
    cuts = [(first_block - 1, first_block + 3.7), (first_beam - .45, first_beam + 3.5)]
    pieces = []
    for i, (start, end) in enumerate(cuts):
        pieces += [f'[0:v]trim=start={start}:end={end},setpts=PTS-STARTPTS[v{i}]',
                   f'[0:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS[a{i}]']
    pieces += ['[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]']
    subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(output),
                    '-filter_complex', ';'.join(pieces), '-map', '[v]', '-map', '[a]', '-c:v', 'libx264',
                    '-crf', '19', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-movflags', '+faststart',
                    str(folder / 'battle-highlights.mp4')], check=True)
    if args.baseline is not None:
        newer = root / 'artifacts/rigged-combat'
        for i in range(300):
            for directory in (args.baseline, newer):
                if not (directory / f'frame-{i:04d}.png').is_file():
                    parser.error(f'missing comparison frame {i} in {directory}')
        subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '30',
                        '-i', str(args.baseline / 'frame-%04d.png'), '-framerate', '30',
                        '-i', str(newer / 'frame-%04d.png'), '-filter_complex',
                        '[0:v]scale=640:360[a];[1:v]scale=640:360[b];[a][b]hstack=inputs=2[v]',
                        '-map', '[v]', '-t', '10', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                        '-metadata', 'comment=Left: bdbb1fa baseline. Right: cinematic upgrade. Same input sequence.',
                        '-movflags', '+faststart', str(folder / 'before-after.mp4')], check=True)
    print(f'{output} ({duration:.2f}s)')


if __name__ == '__main__':
    main()
