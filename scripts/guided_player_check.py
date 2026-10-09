"""Exercise the real gesture receiver, whole battle, automatic photo and hand menus.

Only generated poses/person pixels are sent. No keyboard/mouse events or health
edits; the actual game writes TEST photos to Downloads, which are moved into the
ignored proof directory after validation.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import statistics
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


def selection_landmarks(direction):
    """Return a single raised hand pose for the waiting-page hero carousel."""
    points=landmarks_at(0)
    if direction=='left':
        points[15].x,points[15].y=.90,.02
    else:
        points[16].x,points[16].y=.10,.02
    return points


def tempo_landmarks(seconds):
    """Alternate real receiver trajectories: slow left, fast right, with retraction."""
    points=landmarks_at(0)
    for point in points:point.z=-.1
    for index,x,y,z in [(11,.65,.35,-.1),(12,.35,.35,-.1),(13,.68,.50,-.12),
                        (14,.32,.50,-.12),(15,.58,.44,-.20),(16,.42,.44,-.20)]:
        points[index].x,points[index].y,points[index].z=x,y,z
    phase=seconds%2.65
    right=phase>=1.55
    if right:phase-=1.55
    duration=.14 if right else .65
    active=phase-.18
    extension=max(0,min(1,active/duration))
    if active>duration+.16:extension=max(0,1-(active-duration-.16)/.30)
    hand=points[16 if right else 15]
    hand.x+=(-.04 if right else .04)*extension;hand.y-=.04*extension;hand.z-=.28*extension
    return points


def read_process_memory(pid):
    """Read one macOS process sample without turning a missing process into zero."""
    try:
        result=subprocess.run(['ps','-o','rss=','-o','vsz=','-o','command=','-p',str(pid)],
            check=False,capture_output=True,text=True)
    except OSError:
        return None,None,''
    line=result.stdout.strip()
    if result.returncode!=0 or not line:return None,None,''
    fields=line.split(maxsplit=2)
    if len(fields)<3:return None,None,''
    try:return int(fields[0]),int(fields[1]),fields[2]
    except ValueError:return None,None,fields[2]


def summarize_memory(rows):
    valid=[row for row in rows if row['rss_kib'] is not None]
    if not valid:
        return {'samples':0,'summary_samples':0,'rss_mib':{},'vsz_gib':{},'stage_peaks_mib':{}}
    # The first ps sample can race process creation and report a tiny RSS
    # before Unity has mapped its player. Keep that raw row in the TSV, but do
    # not use it as the baseline for the memory summary or delta.
    measured=[row for row in valid if row['rss_kib']>=16*1024] or valid
    rss=[row['rss_kib'] for row in measured];vsz=[row['vsz_kib'] for row in measured if row['vsz_kib'] is not None]
    stages={}
    for row in measured:
        stages[row['stage']]=max(stages.get(row['stage'],0),row['rss_kib'])
    return {'samples':len(valid),'summary_samples':len(measured),
        'rss_mib':{'first':round(rss[0]/1024,1),'peak':round(max(rss)/1024,1),'last':round(rss[-1]/1024,1),
                   'delta_last_minus_first':round((rss[-1]-rss[0])/1024,1)},
        'vsz_gib':{'first':round(vsz[0]/1024/1024,2),'peak':round(max(vsz)/1024/1024,2),'last':round(vsz[-1]/1024/1024,2)} if vsz else {},
        'stage_peaks_mib':{stage:round(value/1024,1) for stage,value in stages.items()}}


def write_memory_report(path,rows):
    path.parent.mkdir(parents=True,exist_ok=True)
    with path.open('w',newline='') as handle:
        writer=csv.DictWriter(handle,fieldnames=['elapsed_s','stage','phase','photos_seen','rss_kib','vsz_kib','command'],delimiter='\t')
        writer.writeheader();writer.writerows(rows)
    summary_path=path.with_name(path.stem+'-summary.json')
    summary_path.write_text(json.dumps(summarize_memory(rows),ensure_ascii=False,indent=2)+'\n')


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/guided-arcade')
    parser.add_argument('--log',type=Path,default=ROOT/'logs/guided-player.log')
    parser.add_argument('--memory-output',type=Path,help='Write same-process RSS/VSZ samples as TSV and a summary JSON')
    parser.add_argument('--memory-interval',type=float,default=1.0,help='Seconds between process memory samples')
    parser.add_argument('--gesture-wobble',action='store_true',help='Inject wrist-depth noise into held defense and finisher poses')
    parser.add_argument('--tempo-skills',action='store_true',help='Alternate slow and fast forward punches through the real pose receiver')
    parser.add_argument('--volley-camera',action='store_true',help='Require actual ranged medium-shot and defence handoff screenshots')
    parser.add_argument('--hero',choices=('Tiga','Mebius','Zero','Geed','Grigio'),help='Use the initial left selection gesture to choose this actual hero')
    parser.add_argument('--gesture-entry-noise',action='store_true',help='Also bias initial guard depth and the first frames of a beam hold')
    parser.add_argument('--gesture-shape-noise',action='store_true',help='Move an established guard just beyond its acquisition boundary while injecting depth noise')
    parser.add_argument('--gesture-startup-noise',action='store_true',help='Hide a wrist before guard confirmation and bias finisher depth from its first frame')
    options=parser.parse_args()
    if options.memory_interval<=0:parser.error('--memory-interval must be greater than zero')
    if options.gesture_entry_noise and not options.gesture_wobble:parser.error('--gesture-entry-noise requires --gesture-wobble')
    if options.volley_camera and not options.tempo_skills:parser.error('--volley-camera requires --tempo-skills')
    if options.gesture_shape_noise and not options.gesture_wobble:parser.error('--gesture-shape-noise requires --gesture-wobble')
    if options.gesture_startup_noise and not options.gesture_entry_noise:parser.error('--gesture-startup-noise requires --gesture-entry-noise')
    app=ROOT/'unity/Builds/TigaTraining.app'
    binary=app/'Contents/MacOS'/plistlib.loads((app/'Contents/Info.plist').read_bytes())['CFBundleExecutable']
    log=options.log.resolve();log.parent.mkdir(parents=True,exist_ok=True);log.write_text('')
    folder=options.output.resolve();folder.mkdir(parents=True,exist_ok=True)
    memory_output=options.memory_output.resolve() if options.memory_output else None
    if memory_output:
        memory_output.unlink(missing_ok=True)
        memory_output.with_name(memory_output.stem+'-summary.json').unlink(missing_ok=True)
    def build_hashes():
        data=app/'Contents/Resources/Data'
        return {key:hashlib.sha256((data/file).read_bytes()).hexdigest()
                for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]}
    build={'started_utc':datetime.now(timezone.utc).isoformat(),'before':build_hashes()}
    for name in ('guided-validation.json','build.json'):(folder/name).unlink(missing_ok=True)
    factory=FrameFactory(source='synthetic');process=None;photos=[];pose_trace=[];memory_rows=[]
    with PoseBridge(0) as bridge,GamePreview(0) as preview,GamePhoto(0) as photo:
        args=[str(binary),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
            '-logFile',str(log),'--guided-proof','--proof-output',str(folder/'native'),'--pose-port',str(bridge.address[1]),
            '--preview-port',str(preview.bridge.address[1]),'--photo-port',str(photo.bridge.address[1])]
        if options.hero:
            # Start on the next available card: the existing initial left-hand
            # gesture must select the requested hero through the real carousel.
            next_card={'Tiga':'Mebius','Mebius':'Zero','Zero':'Geed','Geed':'Grigio','Grigio':'Tiga'}
            args+=['--proof-hero',next_card[options.hero]]
        try:
            process=subprocess.Popen(args,cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            started=time.monotonic();parsed=0;phase='Waiting';stage='battle';beam=False;guard=False;review_at=0
            photos_seen=0;interrupted=False;loss_start=0;replayed=False;review_loss_start=0;replay_battle_at=0;replay_started_at=0
            initial_selection_done=False;initial_selection_at=0;replay_selection_sent=False;replay_selection_seen=False
            replay_selection_direction='right'
            beam_release_until=0
            pose_contexts={};gesture_audit=[];guard_entry_contexts=set()
            guard_started=0;protected='';unwanted_attacks=0;guard_noise_frames=beam_noise_frames=guard_overlap_frames=0
            guard_entries=beam_entry_noise_frames=0
            guard_shape_frames=guard_reacquisitions=0;guard_confirmed=False
            guard_startup_overlap_frames=beam_startup_frames=0
            next_memory_sample=0.0
            tempo_started=None
            while time.monotonic()-started<240:
                if process.poll() is not None: raise RuntimeError('Player ended early')
                now=time.monotonic();age=now-started;output=log.read_text(errors='replace')
                complete=output.rfind('\n')+1;lines=output[parsed:complete].splitlines();parsed=complete
                for line in lines:
                    hero_match=re.search(r'\[HeroAtlasLights\] hero=([A-Za-z0-9]+)',line)
                    if hero_match:
                        # The release build remembers the last playable hero.
                        # Grigio is the last currently available card, so a
                        # right wave would land on the intentionally unavailable
                        # Zeta placeholder and produce no selection event.
                        replay_selection_direction='left' if hero_match.group(1)=='Grigio' else 'right'
                    if options.gesture_wobble and '[Gesture]' in line and '挥拳' in line:
                        # Log delivery is asynchronous. Attribute each decision
                        # to the pose packet it consumed, not the pose currently
                        # being sent after a Warning/Beam transition.
                        match=re.search(r'sequence=(\d+)',line)
                        sequence=int(match.group(1)) if match else -1
                        if sequence not in pose_contexts:
                            raise RuntimeError(f'Unattributed punch sequence: {sequence}')
                        source_protected=pose_contexts[sequence]
                        gesture_audit.append(dict(sequence=sequence,source_protected=source_protected,
                                                  delivery_protected=protected,event=line))
                        if source_protected:unwanted_attacks+=1
                    if options.gesture_entry_noise and '[Gesture] 护盾已展开' in line:
                        match=re.search(r'sequence=(\d+)',line)
                        if match and int(match.group(1)) in guard_entry_contexts:guard_entries+=1
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
                        if cue=='Warning':
                            guard=True;guard_started=now+(.30 if options.tempo_skills else 0);guard_confirmed=False
                        if cue in ('Block','Hurt'):guard=False
                        # Tracking loss cancels the game's unfinished warning.
                        # Do not keep holding a guard for an attack that no longer exists.
                        if cue=='Resume':guard=False
                        if cue=='Transform' and stage=='replay':replayed=True
                        if cue=='BattleStart' and replayed:replay_battle_at=now
                    if '[Photo] automatic live viewfinder opened' in line:stage='photo'
                    if '[Photo] automatic capture complete' in line:
                        photos_seen+=1;stage='review';review_at=now
                    if '[Photo] gesture=play-again' in line:
                        stage='replay';replay_started_at=now
                    if '[HeroSelection]' in line:
                        if stage=='replay':replay_selection_seen=True
                        elif options.hero:
                            chosen=re.search(r'id=([A-Za-z0-9]+)',line)
                            if not chosen or chosen[1]!=options.hero:
                                raise RuntimeError('Initial gesture selected the wrong hero: '+line)
                            initial_selection_at=now
                if memory_output and age>=next_memory_sample:
                    rss_kib,vsz_kib,command=read_process_memory(process.pid)
                    memory_rows.append({'elapsed_s':round(age,3),'stage':stage,'phase':phase,
                        'photos_seen':photos_seen,'rss_kib':rss_kib,'vsz_kib':vsz_kib,'command':command})
                    next_memory_sample=age+options.memory_interval
                if options.gesture_startup_noise and unwanted_attacks:
                    raise RuntimeError(f'Gesture startup attack: count={unwanted_attacks} protected={protected}')
                if options.gesture_shape_noise and guard_reacquisitions:
                    raise RuntimeError(f'Guard dropped and reacquired during protected pose: count={guard_reacquisitions}')
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
                elif stage=='replay':
                    # The game deliberately gates a new round behind a fresh
                    # hands-down release and the spoken "举起双手" cue. Keep
                    # the synthetic player neutral until the audio window has
                    # elapsed; holding the transform pose from the photo review
                    # would correctly be rejected as stale input. Then exercise
                    # one deliberate carousel gesture before transforming, so
                    # the release path proves that a photo replay restores
                    # selection ownership as well as the transform gate.
                    replay_age=now-replay_started_at if replay_started_at else 0
                    # Memory sampling and the first Metal frame can briefly
                    # stall the player for more than one render interval. Keep
                    # the deliberate carousel pose visible long enough for a
                    # fresh packet to reach HeroSelectionGesture; this does
                    # not alter the game's own hold/release rules.
                    if replay_age<6.0:
                        points=landmarks_at(0)
                    elif replay_age<8.8:
                        replay_selection_sent=True;points=selection_landmarks(replay_selection_direction)
                    elif replay_age<9.8:
                        points=landmarks_at(0)
                    else:
                        points=landmarks_at(2.5)
                elif phase=='Waiting':
                    # Consume the initial selection latch once, then release
                    # before the first transform. This makes the later
                    # post-photo reset assertion meaningful in the same run.
                    if options.hero and not initial_selection_done:
                        # Imported models can finish loading after the old 4.1 s
                        # selection window. Wait for the actual selection event,
                        # then release before transforming; never test the neighbour.
                        if not initial_selection_at:points=selection_landmarks('left')
                        elif now-initial_selection_at<.8:points=landmarks_at(0)
                        else:
                            initial_selection_done=True;points=landmarks_at(2.5)
                    elif not initial_selection_done:
                        if age<4.1:points=selection_landmarks('left')
                        elif age<4.8:points=landmarks_at(0)
                        else:
                            initial_selection_done=True;points=landmarks_at(2.5)
                    elif age>3:points=landmarks_at(2.5)
                elif phase=='Battle' and stage=='battle':
                    if beam and now<beam_release_until:
                        # A short neutral/retract window arms the next beam.
                        points=landmarks_at(4.15)
                    else:
                        points=landmarks_at(13.5 if beam else 10.5 if guard else 4+(age%1.1)/1.1*2)
                        if options.tempo_skills and not beam and not guard:
                            if tempo_started is None:tempo_started=now
                            points=tempo_landmarks(now-tempo_started)
                        else:tempo_started=None
                        if options.gesture_wobble and beam:
                            points[15].z=points[16].z=-.10
                            hold_age=now-beam_release_until
                            if options.gesture_startup_noise:
                                # A clear upright forearm, biased before the
                                # first valid hold: no clean depth seed frame.
                                points[15].y=.245;points[15].z=-.53
                                beam_startup_frames+=1
                            if options.gesture_entry_noise and .04<hold_age<.29:
                                points[15].z=-.43;beam_entry_noise_frames+=1
                            if .30<hold_age<.59:
                                points[15].z=-.70;beam_noise_frames+=1
                                if options.gesture_startup_noise:points[15].visibility=.1
                            if hold_age>(0 if options.gesture_entry_noise else .25):protected='beam'
                        elif options.gesture_wobble and guard:
                            points[15].z=points[16].z=-.10
                            if options.gesture_shape_noise:
                                points[15].x=.73;points[16].x=.27
                                points[15].y=points[16].y=.39
                            if options.gesture_entry_noise and now-guard_started<=.7:
                                points[15].z=-.44;protected='shield'
                            if options.gesture_startup_noise and .06<now-guard_started<.18:
                                points[16].visibility=.1;guard_startup_overlap_frames+=1
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
                if options.tempo_skills and guard and not beam and now<guard_started:
                    # The tempo fixture rests at chest height and can already
                    # be guarding before Warning. Release that old guard so
                    # this audit exercises a new, initially biased acquisition.
                    points=landmarks_at(0);protected='';tempo_started=None
                if stage=='photo' and not interrupted and '[Photo] countdown=4' in output:
                    interrupted=True;loss_start=now
                if stage=='review' and photos_seen==2 and now-review_at>7 and not review_loss_start:
                    review_loss_start=now
                    print('[GuidedRecovery] interrupt second-review poses for 450 ms after hands-down release',flush=True)
                # Stop just the photo stream mid-countdown; ordinary pose/preview keep running.
                publish_at=time.monotonic()
                frame=factory.make(points)
                if options.gesture_wobble:pose_contexts[frame['sequence']]=protected
                if options.gesture_entry_noise and guard and not beam and 0<=now-guard_started<.7:
                    guard_entry_contexts.add(frame['sequence'])
                if options.gesture_startup_noise or options.tempo_skills:
                    pose_trace.append(dict(frame=frame,protected=protected,beam_available=beam,transform_available=phase=='Waiting'))
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
            if not replay_selection_sent or not replay_selection_seen:
                raise RuntimeError(f'Photo replay did not restore hero selection: sent={replay_selection_sent} seen={replay_selection_seen}')
            required=['live cutout displayed','countdown interrupted','gesture=retake','gesture=play-again','automatic capture complete']
            for marker in required:
                if marker not in output:raise RuntimeError('Missing '+marker)
            if re.search(r'NullReferenceException|Shader error|error CS\d',output):raise RuntimeError('Unity runtime error')
            tempo_evidence=None
            hero_evidence=re.findall(r'\[GuidedProofHero\] battle=([A-Za-z0-9]+)',output)
            if options.hero and (not hero_evidence or hero_evidence[0]!=options.hero):
                raise RuntimeError(f'Requested hero was not selected through gesture: {options.hero}/{hero_evidence}')
            if options.tempo_skills:
                attacks=re.findall(r'\[AttackTempo\] ranged=True speed=([\d.]+) duration=([\d.]+) side=(LeftPunch|RightPunch)',output)
                launches=re.findall(r'\[HeroProjectile\] launch side=(LeftPunch|RightPunch) speed=([\d.]+) sequence=(\d+)',output)
                speeds=[float(row[0]) for row in attacks]
                if len(attacks)<20 or {row[2] for row in attacks}!={'LeftPunch','RightPunch'} or max(speeds)<min(speeds)*1.3:
                    raise RuntimeError(f'Missing actual slow/fast ranged receiver evidence: {attacks}')
                slow=statistics.median(float(row[0]) for row in attacks if row[2]=='LeftPunch')
                fast=statistics.median(float(row[0]) for row in attacks if row[2]=='RightPunch')
                if fast<slow*1.25:raise RuntimeError(f'Fast trajectory did not speed up the actual player: {slow}/{fast}')
                if len(launches)<20 or len({row[2] for row in launches})!=len(launches):
                    raise RuntimeError(f'Missing or duplicate remote launches: {launches}')
                proof_shots={path.stem for path in (folder/'native').glob('*.png')}
                if not {'light-bullet-left','light-bullet-right'}.issubset(proof_shots):
                    raise RuntimeError('Missing rendered remote shots from both hands')
                impacts=re.findall(r'\[RangedPresentation\] impact hero=([A-Za-z0-9]+) count=(\d+) visible=True',output)
                if len(impacts)!=len(launches) or not {'ranged-launch-volume','ranged-impact-volume'}.issubset(proof_shots):
                    raise RuntimeError(f'Missing remote contact volumes: impacts={len(impacts)} launches={len(launches)}')
                if options.hero=='Zero' and not {'zero-sluggers-out','zero-sluggers-return'}.issubset(proof_shots):
                    raise RuntimeError('Actual Zero player did not capture both blade directions')
                tempo_evidence=dict(accepted=len(attacks),launches=len(launches),speed_min=min(speeds),speed_max=max(speeds),
                    slow_median=slow,fast_median=fast,
                    hands=sorted({row[2] for row in attacks}),impacts=len(impacts),shots=sorted(proof_shots&{'light-bullet-left','light-bullet-right','ranged-launch-volume','ranged-impact-volume','zero-sluggers-out','zero-sluggers-return'}))
                if options.volley_camera:
                    required={'ranged-volley-peak','ranged-volley-yield','ranged-volley-return'}
                    if not required.issubset(proof_shots):raise RuntimeError(f'Missing actual volley camera proof: {required-proof_shots}')
                    peaks=re.findall(r'\[RangedCameraProof\] ranged-volley-peak focus=([\d.]+)',output)
                    if not peaks or max(map(float,peaks))<.97:raise RuntimeError('Actual player did not reach the volley lens')
                    tempo_evidence['volley_camera']=dict(shots=sorted(required),focus_max=max(map(float,peaks)))
            if options.gesture_wobble:
                if unwanted_attacks or guard_noise_frames<4 or beam_noise_frames<4 or guard_overlap_frames<4:
                    raise RuntimeError(f'Gesture arbitration failed: unwanted={unwanted_attacks} guardNoise={guard_noise_frames} beamNoise={beam_noise_frames} guardOverlap={guard_overlap_frames}')
                if output.count('[Game] cue=Block ')<1 or output.count('[Game] cue=Beam ')<2:
                    raise RuntimeError('Wobbled input did not complete both defense and finishers')
                if options.gesture_entry_noise and (guard_entries<1 or beam_entry_noise_frames<4):
                    raise RuntimeError(f'Initial gesture ownership not verified: guards={guard_entries} beamNoise={beam_entry_noise_frames}')
                if options.gesture_shape_noise and (guard_shape_frames<4 or guard_reacquisitions):
                    raise RuntimeError(f'Guard shape ownership failed: driftFrames={guard_shape_frames} reacquisitions={guard_reacquisitions}')
                if options.gesture_startup_noise and (guard_startup_overlap_frames<4 or beam_startup_frames<20):
                    raise RuntimeError(f'Gesture startup not exercised: guardOverlap={guard_startup_overlap_frames} beamBias={beam_startup_frames}')
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
                'replay_battle_started':True,'replay_selection_reset':True,
                'photo_preview_p99_error':preview_error,'photos':photos}
            if options.gesture_wobble:result['gesture_wobble']={'unwanted_attacks':unwanted_attacks,'guard_noise_frames':guard_noise_frames,'guard_overlap_frames':guard_overlap_frames,'beam_noise_frames':beam_noise_frames,'blocks':output.count('[Game] cue=Block '),'beams':output.count('[Game] cue=Beam ')}
            if tempo_evidence:result['tempo_skills']=tempo_evidence
            result['battle_heroes']=hero_evidence
            if options.gesture_entry_noise:result['gesture_entry_noise']={'guards_acquired_before_700ms':guard_entries,'beam_noise_frames':beam_entry_noise_frames}
            if options.gesture_shape_noise:result['gesture_shape_noise']={'frames':guard_shape_frames,'unexpected_reacquisitions':guard_reacquisitions}
            if options.gesture_startup_noise:result['gesture_startup_noise']={'guard_overlap_frames':guard_startup_overlap_frames,'beam_bias_frames':beam_startup_frames}
            if memory_output:result['memory']=summarize_memory(memory_rows)
            (folder/'guided-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
            print(json.dumps(result,ensure_ascii=False),flush=True)
        finally:
            stop_process(process)
            if options.gesture_wobble:
                (folder/'gesture-audit.json').write_text(json.dumps(dict(poses=pose_contexts,events=gesture_audit,guard_entry_sequences=sorted(guard_entry_contexts)),ensure_ascii=False,indent=2)+'\n')
            if options.gesture_startup_noise or options.tempo_skills:
                (folder/'synthetic-poses.jsonl').write_text(''.join(json.dumps(row,separators=(',',':'))+'\n' for row in pose_trace))
            if memory_output:write_memory_report(memory_output,memory_rows)
            build['after']=build_hashes()
            (folder/'build.json').write_text(json.dumps(build,indent=2)+'\n')

if __name__=='__main__':main()
