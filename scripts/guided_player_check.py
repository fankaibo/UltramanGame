"""Exercise the real gesture receiver, whole battle, automatic photo and hand menus.

Only generated poses/person pixels are sent. No keyboard/mouse events or health
edits; the actual game writes TEST photos to Downloads, which are moved into the
ignored proof directory after validation.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from vision.bridge import PoseBridge
from vision.protocol import FrameFactory
from vision.demo import landmarks_at
from vision.preview import GamePreview
from vision.photo import GamePhoto
from vision.supervision import stop_process


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/guided-arcade')
    parser.add_argument('--log',type=Path,default=ROOT/'logs/guided-player.log')
    parser.add_argument('--gesture-wobble',action='store_true',help='Inject wrist-depth noise into held defense and finisher poses')
    parser.add_argument('--gesture-entry-noise',action='store_true',help='Also bias initial guard depth and the first frames of a beam hold')
    parser.add_argument('--gesture-shape-noise',action='store_true',help='Move an established guard just beyond its acquisition boundary while injecting depth noise')
    options=parser.parse_args()
    if options.gesture_entry_noise and not options.gesture_wobble:parser.error('--gesture-entry-noise requires --gesture-wobble')
    if options.gesture_shape_noise and not options.gesture_wobble:parser.error('--gesture-shape-noise requires --gesture-wobble')
    app=ROOT/'unity/Builds/TigaTraining.app'
    binary=app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    log=options.log.resolve();log.parent.mkdir(parents=True,exist_ok=True);log.write_text('')
    folder=options.output.resolve();folder.mkdir(parents=True,exist_ok=True)
    def build_hashes():
        data=app/'Contents/Resources/Data'
        return {key:hashlib.sha256((data/file).read_bytes()).hexdigest()
                for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]}
    build={'started_utc':datetime.now(timezone.utc).isoformat(),'before':build_hashes()}
    for name in ('guided-validation.json','build.json'):(folder/name).unlink(missing_ok=True)
    factory=FrameFactory(source='synthetic');process=None;photos=[]
    with PoseBridge(0) as bridge,GamePreview(0) as preview,GamePhoto(0) as photo:
        args=[str(binary),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
            '-logFile',str(log),'--guided-proof','--proof-output',str(folder/'native'),'--pose-port',str(bridge.address[1]),
            '--preview-port',str(preview.bridge.address[1]),'--photo-port',str(photo.bridge.address[1])]
        try:
            process=subprocess.Popen(args,cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            started=time.monotonic();parsed=0;phase='Waiting';stage='battle';beam=False;guard=False;review_at=0
            photos_seen=0;interrupted=False;loss_start=0;replayed=False;review_loss_start=0;replay_battle_at=0
            beam_release_until=0
            guard_started=0;protected='';unwanted_attacks=0;guard_noise_frames=beam_noise_frames=guard_overlap_frames=0
            guard_entries=beam_entry_noise_frames=0
            guard_shape_frames=guard_reacquisitions=0;guard_confirmed=False
            while time.monotonic()-started<240:
                if process.poll() is not None: raise RuntimeError('Player ended early')
                now=time.monotonic();age=now-started;output=log.read_text(errors='replace')
                complete=output.rfind('\n')+1;lines=output[parsed:complete].splitlines();parsed=complete
                for line in lines:
                    if options.gesture_wobble and protected and '[Gesture]' in line and '挥拳' in line:
                        unwanted_attacks+=1
                    if options.gesture_entry_noise and guard_started and now-guard_started<.7 and '[Gesture] 护盾已展开' in line:
                        guard_entries+=1
                    if options.gesture_shape_noise and guard and not beam and '[Gesture] 护盾已展开' in line:
                        if guard_confirmed:guard_reacquisitions+=1
                        guard_confirmed=True
                    if '[Game] cue=' in line:
                        match=re.search(r'cue=(\w+) phase=(\w+)',line)
                        cue,phase=match.groups()
                        if cue=='EnergyReady':
                            beam=True
                            # Match the player's spoken instruction: release the
                            # previous attack/guard pose before holding the beam.
                            # The runtime intentionally requires this release so a
                            # held pose cannot fire a second beam accidentally.
                            beam_release_until=now+.75
                        if cue in ('Beam','Victory'):beam=False;guard=False
                        if cue=='Warning':guard=True;guard_started=now;guard_confirmed=False
                        if cue in ('Block','Hurt'):guard=False
                        # Tracking loss cancels the game's unfinished warning.
                        # Do not keep holding a guard for an attack that no longer exists.
                        if cue=='Resume':guard=False
                        if cue=='Transform' and stage=='replay':replayed=True
                        if cue=='BattleStart' and replayed:replay_battle_at=now
                    if '[Photo] automatic live viewfinder opened' in line:stage='photo'
                    if '[Photo] automatic capture complete' in line:
                        photos_seen+=1;stage='review';review_at=now
                    if '[Photo] gesture=play-again' in line:stage='replay'
                # Continue through transformation so the resumed arena must render
                # again after its camera was disabled during the full-screen photo.
                if replay_battle_at and now-replay_battle_at>2:break
                points=landmarks_at(0)
                protected=''
                if stage=='review':
                    # Wait through spoken preview instructions, lower hands to rearm,
                    # then retake once and select another round with both hands.
                    if now-review_at<5:
                        points[15].visibility=points[16].visibility=.1
                    if now-review_at>6:
                        points=landmarks_at(2.5)
                        if photos_seen==1:
                            points[16].y=.61
                elif stage=='replay' or phase=='Waiting':
                    if age>3:points=landmarks_at(2.5)
                elif phase=='Battle' and stage=='battle':
                    if beam and now<beam_release_until:
                        # A short neutral/retract window arms the next beam.
                        points=landmarks_at(4.15)
                    else:
                        points=landmarks_at(13.5 if beam else 10.5 if guard else 4+(age%1.1)/1.1*2)
                        if options.gesture_wobble and beam:
                            points[15].z=points[16].z=-.10
                            hold_age=now-beam_release_until
                            if options.gesture_entry_noise and .04<hold_age<.29:
                                points[15].z=-.43;beam_entry_noise_frames+=1
                            if .30<hold_age<.59:
                                points[15].z=-.70;beam_noise_frames+=1
                            if hold_age>(0 if options.gesture_entry_noise else .25):protected='beam'
                        elif options.gesture_wobble and guard:
                            points[15].z=points[16].z=-.10
                            if options.gesture_shape_noise:
                                points[15].x=.73;points[16].x=.27
                                points[15].y=points[16].y=.39
                            if options.gesture_entry_noise and now-guard_started<=.7:
                                points[15].z=-.44;protected='shield'
                            if now-guard_started>.7:
                                protected='shield'
                                if (now-guard_started)%.4<.14:
                                    if options.gesture_shape_noise:
                                        points[15].x=.747;points[16].x=.253;guard_shape_frames+=1
                                    hand=15 if int((now-guard_started)/.4)%2==0 else 16
                                    points[hand].z=-.44;guard_noise_frames+=1
                                    # Protect a briefly hidden opposite wrist too,
                                    # as happens when crossed hands overlap on camera.
                                    # Alternate with depth-only noise to exercise both.
                                    if int((now-guard_started)/.8)%2==0:
                                        points[16 if hand==15 else 15].visibility=.1
                                        guard_overlap_frames+=1
                if stage=='photo' and not interrupted and '[Photo] countdown=4' in output:
                    interrupted=True;loss_start=now
                if stage=='review' and photos_seen==2 and now-review_at>7 and not review_loss_start:
                    review_loss_start=now
                    print('[GuidedRecovery] interrupt second-review poses for 450 ms after hands-down release',flush=True)
                # Stop just the photo stream mid-countdown; ordinary pose/preview keep running.
                publish_at=time.monotonic()
                frame=factory.make(points)
                if not review_loss_start or now-review_loss_start>.45:bridge.publish(frame)
                pose_at=time.monotonic()
                preview.publish(None,points,frame['capturedMs'],'synthetic')
                preview_at=time.monotonic()
                if not loss_start or now-loss_start>1.3:
                    photo.publish(None,None,frame['capturedMs'],synthetic=True)
                photo_at=time.monotonic()
                if photo_at-publish_at>.2:
                    print(f'[GuidedLatency] stage={stage} age={age:.1f} poseMs={(pose_at-publish_at)*1000:.0f} previewMs={(preview_at-pose_at)*1000:.0f} photoMs={(photo_at-preview_at)*1000:.0f}',flush=True)
                time.sleep(1/30)
            if not replay_battle_at:raise RuntimeError('Guided loop did not complete within 240 seconds')
            if not review_loss_start:raise RuntimeError('Second-review pose dropout was not exercised')
            required=['live cutout displayed','countdown interrupted','gesture=retake','gesture=play-again','automatic capture complete']
            for marker in required:
                if marker not in output:raise RuntimeError('Missing '+marker)
            if re.search(r'NullReferenceException|Shader error|error CS\d',output):raise RuntimeError('Unity runtime error')
            if options.gesture_wobble:
                if unwanted_attacks or guard_noise_frames<4 or beam_noise_frames<4 or guard_overlap_frames<4:
                    raise RuntimeError(f'Gesture arbitration failed: unwanted={unwanted_attacks} guardNoise={guard_noise_frames} beamNoise={beam_noise_frames} guardOverlap={guard_overlap_frames}')
                if output.count('[Game] cue=Block ')<1 or output.count('[Game] cue=Beam ')<2:
                    raise RuntimeError('Wobbled input did not complete both defense and finishers')
                if options.gesture_entry_noise and (guard_entries<1 or beam_entry_noise_frames<4):
                    raise RuntimeError(f'Initial gesture ownership not verified: guards={guard_entries} beamNoise={beam_entry_noise_frames}')
                if options.gesture_shape_noise and (guard_shape_frames<4 or guard_reacquisitions):
                    raise RuntimeError(f'Guard shape ownership failed: driftFrames={guard_shape_frames} reacquisitions={guard_reacquisitions}')
            filenames=re.findall(r'\[Photo\] saved source=synthetic size=1920x1080 file=(.+)',output)
            if len(filenames)!=2 or len(set(filenames))!=2:raise RuntimeError('Expected two distinct TEST photos')
            for name in filenames:
                path=Path.home()/'Downloads'/name.strip()
                data=path.read_bytes()
                if data[:8]!=b'\x89PNG\r\n\x1a\n':raise RuntimeError('Invalid saved PNG')
                destination=folder/path.name;path.replace(destination);photos.append(str(destination))
            with Image.open(photos[0]) as saved,Image.open(folder/'native/photo-Review.png') as screen:
                if saved.size!=screen.size:raise RuntimeError('Photo preview/export resolution mismatch')
                # Exclude the title and footer controls. The settled central
                # review should display exactly the image saved to Downloads.
                region=(int(saved.width*.05),int(saved.height*.15),int(saved.width*.91),int(saved.height*.83))
                difference=np.abs(np.asarray(saved.convert('RGB').crop(region),dtype=np.int16)-np.asarray(screen.convert('RGB').crop(region),dtype=np.int16))
                preview_error=float(np.percentile(difference,99))
                if preview_error>2:raise RuntimeError(f'Photo preview differs from exported PNG: p99={preview_error}')
            result={'result':'passed','seconds':round(time.monotonic()-started,1),'input':'synthetic camera poses',
                'keyboard_mouse_events':0,'real_images_saved':0,'automatic_photos':2,'retake':True,'play_again':True,
                'photo_dropout_recovered':True,'review_pose_dropout_recovered':bool(review_loss_start),
                'replay_battle_started':True,'photo_preview_p99_error':preview_error,'photos':photos}
            if options.gesture_wobble:result['gesture_wobble']={'unwanted_attacks':unwanted_attacks,'guard_noise_frames':guard_noise_frames,'guard_overlap_frames':guard_overlap_frames,'beam_noise_frames':beam_noise_frames,'blocks':output.count('[Game] cue=Block '),'beams':output.count('[Game] cue=Beam ')}
            if options.gesture_entry_noise:result['gesture_entry_noise']={'guards_acquired_before_700ms':guard_entries,'beam_noise_frames':beam_entry_noise_frames}
            if options.gesture_shape_noise:result['gesture_shape_noise']={'frames':guard_shape_frames,'unexpected_reacquisitions':guard_reacquisitions}
            (folder/'guided-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
            print(json.dumps(result,ensure_ascii=False),flush=True)
        finally:
            stop_process(process)
            build['after']=build_hashes()
            (folder/'build.json').write_text(json.dumps(build,indent=2)+'\n')

if __name__=='__main__':main()
