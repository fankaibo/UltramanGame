"""Compare same-input camera responses and encode the complete contact sequences."""
import csv
import hashlib
import json
import math
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT/'artifacts/a170-contact-camera'
BASELINE = '38348a808dc00fddfcb8ad9bc52228fbf525e12b'
MODES = ('combo', 'ranged', 'guard', 'hurt')


def main():
    source = 'unity/Assets/Scripts/Runtime/GameWorld.cs'
    before = (FOLDER/'before-GameWorld.cs').read_bytes()
    assert before == subprocess.check_output(['git','show',BASELINE+':'+source],cwd=ROOT)
    rows = 0
    results = []
    state_keys = ('frame','health','energy','action','age','enemy','enemyAge','blocks','hits','contact','combo','exchange')
    for hz in (15,30,60):
        for mode in MODES:
            versions = {}
            for version in ('before','after'):
                folder = FOLDER/version/f'{mode}-{hz}'
                pair = [list(csv.DictReader((folder/f).open())) for f in ('trace.csv','quiet.csv')]
                assert all(len(trace)==hz*4 for trace in pair)
                for a,b in zip(*pair):
                    assert all(a[k]==b[k] for k in state_keys)
                distances = [math.dist([float(a[k]) for k in ('x','y','z')], [float(b[k]) for k in ('x','y','z')]) for a,b in zip(*pair)]
                assert max(distances[-hz//3:]) < .0001, (version, mode, 'recoil persists')
                versions[version] = pair[0]
                results.append(dict(hz=hz,mode=mode,version=version,maximum_contact_displacement=max(distances)))
                if version=='after':
                    assert .008 < max(distances) < .12, (hz,mode,distances)
            for old,new in zip(versions['before'],versions['after']):
                assert all(old[k]==new[k] for k in state_keys)
                rows += 1
    parts=[]
    for mode in MODES:
        for version in ('before','after'):
            assert len(list((FOLDER/version/f'{mode}-60/frames').glob('*.jpg')))==120
        movie=FOLDER/(mode+'-comparison.mp4')
        subprocess.run(['ffmpeg','-y','-v','error','-framerate','30','-i',str(FOLDER/'before'/f'{mode}-60/frames/%04d.jpg'),
                        '-framerate','30','-i',str(FOLDER/'after'/f'{mode}-60/frames/%04d.jpg'),'-filter_complex','[0:v][1:v]hstack=inputs=2[v]',
                        '-map','[v]','-frames:v','120','-c:v','libx264','-crf','19','-pix_fmt','yuv420p',str(movie)],check=True)
        parts.append(movie)
    manifest=FOLDER/'video-parts.txt';manifest.write_text(''.join("file '"+str(p)+"'\n" for p in parts))
    movie=FOLDER/'continuous-before-after.mp4'
    subprocess.run(['ffmpeg','-y','-v','error','-f','concat','-safe','0','-i',str(manifest),'-c','copy','-movflags','+faststart',str(movie)],check=True)
    media=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-of','json',str(movie)],text=True))['streams'][0]
    assert int(media['nb_frames'])==480 and media['width']==2560 and media['height']==720
    (FOLDER/'comparison.json').write_text(json.dumps(dict(baseline=BASELINE,source=source,before_sha256=hashlib.sha256(before).hexdigest(),
        after_sha256=hashlib.sha256((ROOT/source).read_bytes()).hexdigest(),identical_state_rows=rows,response=results,
        frames=480,seconds=16,modes=list(MODES),note='Offline identical-input sequence; not measured runtime FPS'),indent=2)+'\n')
    print('Verified',rows,'state rows; 12 finite recoil responses; encoded 16-second before/after comparison')


if __name__=='__main__':
    main()
