"""Exercise the real gesture receiver, whole battle, automatic photo and hand menus.

Only generated poses/person pixels are sent. No keyboard/mouse events or health
edits; the actual game writes TEST photos to Downloads, which are moved into the
ignored proof directory after validation.
"""
import json
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from vision.bridge import PoseBridge
from vision.protocol import FrameFactory
from vision.demo import landmarks_at
from vision.preview import GamePreview
from vision.photo import GamePhoto
from vision.supervision import stop_process


def main():
    app=ROOT/'unity/Builds/TigaTraining.app'
    binary=app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    log=ROOT/'logs/guided-player.log';log.write_text('')
    folder=ROOT/'artifacts/guided-arcade';folder.mkdir(parents=True,exist_ok=True)
    factory=FrameFactory(source='synthetic');process=None;photos=[]
    with PoseBridge(0) as bridge,GamePreview(0) as preview,GamePhoto(0) as photo:
        args=[str(binary),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
            '-logFile',str(log),'--guided-proof','--proof-output',str(folder/'native'),'--pose-port',str(bridge.address[1]),
            '--preview-port',str(preview.bridge.address[1]),'--photo-port',str(photo.bridge.address[1])]
        try:
            process=subprocess.Popen(args,cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            started=time.monotonic();parsed=0;phase='Waiting';stage='battle';beam=False;guard=False;review_at=0
            photos_seen=0;interrupted=False;loss_start=0;replayed=False
            while time.monotonic()-started<240:
                if process.poll() is not None: raise RuntimeError('Player ended early')
                now=time.monotonic();age=now-started;output=log.read_text(errors='replace')
                complete=output.rfind('\n')+1;lines=output[parsed:complete].splitlines();parsed=complete
                for line in lines:
                    if '[Game] cue=' in line:
                        match=re.search(r'cue=(\w+) phase=(\w+)',line)
                        cue,phase=match.groups()
                        if cue=='EnergyReady':beam=True
                        if cue in ('Beam','Victory'):beam=False;guard=False
                        if cue=='Warning':guard=True
                        if cue in ('Block','Hurt'):guard=False
                        if cue=='Transform' and stage=='replay':replayed=True
                    if '[Photo] automatic live viewfinder opened' in line:stage='photo'
                    if '[Photo] automatic capture complete' in line:
                        photos_seen+=1;stage='review';review_at=now
                    if '[Photo] gesture=play-again' in line:stage='replay'
                if replayed: break
                points=landmarks_at(0)
                if stage=='review':
                    # Wait through spoken preview instructions, lower hands to rearm,
                    # then retake once and select another round with both hands.
                    if now-review_at>24:
                        points=landmarks_at(2.5)
                        if photos_seen==1:
                            points[16].y=.61
                elif stage=='replay' or phase=='Waiting':
                    if age>3:points=landmarks_at(2.5)
                elif phase=='Battle' and stage=='battle':
                    points=landmarks_at(13.5 if beam else 10.5 if guard else 4+(age%1.1)/1.1*2)
                if stage=='photo' and not interrupted and '[Photo] countdown=4' in output:
                    interrupted=True;loss_start=now
                # Stop just the photo stream mid-countdown; ordinary pose/preview keep running.
                frame=factory.make(points);bridge.publish(frame)
                preview.publish(None,points,frame['capturedMs'],'synthetic')
                if not loss_start or now-loss_start>1.3:
                    photo.publish(None,None,frame['capturedMs'],synthetic=True)
                time.sleep(1/30)
            if not replayed:raise RuntimeError('Guided loop did not complete within 240 seconds')
            required=['live cutout displayed','countdown interrupted','gesture=retake','gesture=play-again','automatic capture complete']
            for marker in required:
                if marker not in output:raise RuntimeError('Missing '+marker)
            if re.search(r'NullReferenceException|Shader error|error CS\d',output):raise RuntimeError('Unity runtime error')
            filenames=re.findall(r'\[Photo\] saved source=synthetic size=1920x1080 file=(.+)',output)
            if len(filenames)!=2 or len(set(filenames))!=2:raise RuntimeError('Expected two distinct TEST photos')
            for name in filenames:
                path=Path.home()/'Downloads'/name.strip()
                data=path.read_bytes()
                if data[:8]!=b'\x89PNG\r\n\x1a\n':raise RuntimeError('Invalid saved PNG')
                destination=folder/path.name;path.replace(destination);photos.append(str(destination))
            result={'result':'passed','seconds':round(time.monotonic()-started,1),'input':'synthetic camera poses',
                'keyboard_mouse_events':0,'real_images_saved':0,'automatic_photos':2,'retake':True,'play_again':True,
                'photo_dropout_recovered':True,'photos':photos}
            (folder/'guided-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
            print(json.dumps(result,ensure_ascii=False),flush=True)
        finally:stop_process(process)

if __name__=='__main__':main()
