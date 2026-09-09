"""Encode the editor-only, 20-second sample with the project's local effects."""
from pathlib import Path
import argparse
import shutil
import subprocess


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--ffmpeg',default=shutil.which('ffmpeg'))
    args=parser.parse_args()
    if not args.ffmpeg:parser.error('ffmpeg is required')
    root=Path(__file__).resolve().parents[1]
    folder=root/'artifacts/combat-sample'
    frames=folder/'frames'
    for i in range(600):
        if not (frames/f'frame-{i:04d}.png').is_file():parser.error(f'missing frame {i}')
    audio=root/'unity/Assets/Resources/Audio'
    # Delays correspond to the contact events in CombatSampleReview, not clip starts.
    effects=[('swing.wav',4.31,.55),('impact.wav',4.48,.65),
             ('swing.wav',5.31,.55),('impact.wav',5.48,.75),
             ('enemy_rush.wav',8.5,.65),('shield.wav',8.9,.8),
             ('beam.wav',14,.65),('victory.wav',18.1,.6)]
    sounds=[(audio/name,at,volume) for name,at,volume in effects]
    original=root/'unity/Assets/Resources/Voice/beam_original.aiff'
    if original.is_file():sounds.append((original,12.75,.9))
    cmd=[args.ffmpeg,'-hide_banner','-loglevel','error','-y','-framerate','30','-i',str(frames/'frame-%04d.png'),
         '-stream_loop','-1','-i',str(audio/'music_battle.wav')]
    filters=['[1:a]volume=0.25,atrim=0:20[music]'];mix=['[music]']
    for i,(path,at,volume) in enumerate(sounds,2):
        cmd+=['-i',str(path)]
        filters.append(f'[{i}:a]volume={volume},adelay={round(at*1000)}:all=1[a{i}]');mix.append(f'[a{i}]')
    filters.append(''.join(mix)+f'amix=inputs={len(mix)}:duration=longest:normalize=0,alimiter=limit=0.94,atrim=0:20,afade=t=out:st=19.4:d=0.6[mix]')
    output=folder/'tiga-golza-combat-sample.mp4'
    cmd+=['-filter_complex',';'.join(filters),'-map','0:v','-map','[mix]','-t','20','-c:v','libx264',
          '-crf','20','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-movflags','+faststart',str(output)]
    subprocess.run(cmd,check=True)
    print(output)


if __name__=='__main__':main()
