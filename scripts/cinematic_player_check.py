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
from PIL import Image


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--width',type=int,default=1920);parser.add_argument('--height',type=int,default=1080)
    parser.add_argument('--hero', choices=('Tiga','Mebius','Zero','Geed','Grigio'), default='Tiga')
    parser.add_argument('--slam', action='store_true', help='Wait for the third, ground-slam attack before counterattacking')
    parser.add_argument('--ray', action='store_true', help='Exercise the fourth, head-ray attack before counterattacking')
    parser.add_argument('--linked', action='store_true', help='Queue the first three alternating fists during recovery')
    parser.add_argument('--finisher', action='store_true', help='Use 24 HP so the first beam is the final strike')
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
    # Shader/texture-only fixes can leave the managed assembly unchanged.
    # Tie visual proof to the Resources payload loaded by this player as well.
    resources = app / 'Contents/Resources/Data/resources.assets'
    resources_sha = hashlib.sha256(resources.read_bytes()).hexdigest()
    start = time.monotonic()
    with (root / 'logs/cinematic-player-console.log').open('w') as console:
        player = subprocess.Popen([str(binary), '--keyboard', '--review-playback', '--review-hero', args.hero, '--guided-proof',
                                   '--proof-output', str(native), '-screen-fullscreen', '0',
                                   '-screen-width', str(args.width), '-screen-height', str(args.height), '-logFile', str(log)]+(['--review-slam'] if args.slam else [])+(['--review-ray'] if args.ray else [])+(['--review-linked'] if args.linked else [])+(['--review-finisher'] if args.finisher else []),
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
    if hashlib.sha256(resources.read_bytes()).hexdigest() != resources_sha:
        raise RuntimeError('Player resources changed during the review')
    match = re.search(r'\[FullGameReview\] pass=True[^\n]+', output)
    if code != 0 or not match:
        raise RuntimeError(f'Full player review failed, exit={code}; inspect {log}')
    if re.search(r'(?:NullReferenceException|ArgumentException|Shader error|error CS\d)', output):
        raise RuntimeError(f'Runtime exception in {log}')
    # The current startup path deliberately skips offscreen renders to keep a
    # cold Metal launch responsive. Accept that explicit marker as a valid
    # presentation state; it still separates startup diagnostics from the
    # live round below. Older builds may emit the bounded warmup marker.
    warmup_markers=('[PresentationWarmup] complete','[PresentationWarmup] skipped render warmup')
    warmup_marker=next((marker for marker in warmup_markers if marker in output),None)
    if warmup_marker is None:
        raise RuntimeError('Presentation warmup state was not reported')
    live_output = output.split(warmup_marker, 1)[1]
    beam_impacts = live_output.count('[BeamImpactVolume] begin')
    expected_beams = 1 if args.finisher else 2
    if beam_impacts != expected_beams:
        raise RuntimeError(f'Expected one volume burst per real beam hit, got {beam_impacts}')
    beam_braces = live_output.count('[MonsterBeam] brace ')
    if beam_braces != expected_beams:
        raise RuntimeError(f'Expected matching beam recovery-foot landings, got {beam_braces}')
    reaction_cuts = dict(begins=live_output.count('[BeamReactionCamera] begin'), ends=live_output.count('[BeamReactionCamera] end'))
    if reaction_cuts != dict(begins=expected_beams, ends=expected_beams):
        raise RuntimeError(f'Missing beam reaction shot or return: {reaction_cuts}')
    if output.count('[VolcanoEnvironment] captured=True faces=6 size=128 mipmaps=True') != 1:
        raise RuntimeError('Expected one successful arena reflection capture at startup')
    if f'[FullGameReviewHero] id={args.hero}' not in output:
        raise RuntimeError('Requested hero was not instantiated')
    if '[BeamSurface] torso-anchor=True vertices=3' not in output or 'chest-bone fallback' in output:
        raise RuntimeError('Built player did not bind the beam to the readable monster torso')
    beam_voice = 'beam_original' if args.hero == 'Tiga' else 'beam'
    if output.count(f'[Voice] key={beam_voice} playing=True') != expected_beams:
        raise RuntimeError(f'Expected {expected_beams} {beam_voice} battle cries for {args.hero}')
    if output.count('[VictoryStage] landing-thud playing=True') != 1:
        raise RuntimeError('Expected exactly one landing sound for the defeated monster')
    if live_output.count('[DefeatImpact] begin landing=True') != 1 or live_output.count('[DefeatImpact] sound=True') != 1:
        raise RuntimeError('Defeat dust burst and sound did not follow the single landing')
    if output.count('[MonsterDissolve] begin samples=384') != 1 or output.count('[MonsterDissolve] shimmer playing=True') != 1:
        raise RuntimeError('Expected one surface departure and its sound before the photo')
    stagger_landings = len(re.findall(r'\[MonsterStagger\] landed side=', output))
    if stagger_landings < 2 or output.count('[MonsterStagger] footstep playing=True') != stagger_landings:
        raise RuntimeError('Recovery-step landing and sound did not match')
    combo_shots = output.count('[ComboCamera] begin side=')
    if combo_shots < 2:
        raise RuntimeError('Full round did not exercise multiple combo camera shots')
    # Startup renders a silent beam brace to warm the same contact materials.
    # Only gameplay contacts should have a matching gameplay sound event.
    ground_contacts = re.findall(r'\[GroundImpact\] cause=([^ ]+) ', live_output)
    outpost_shocks = re.findall(r'\[OutpostShock\] cause=([^ ]+) scheduled=(\d+)', live_output)
    if [cause for cause, _ in outpost_shocks] != ground_contacts:
        raise RuntimeError('Outpost response must follow each real ground contact exactly once')
    detached_panels = sum(int(count) for _, count in outpost_shocks)
    if detached_panels < 4:
        raise RuntimeError('Heavy impacts did not detach outpost panels')
    launch_landings = output.count('[MonsterLaunch] landed age=')
    if launch_landings < 1 or ground_contacts.count('uppercut-land') != launch_landings:
        raise RuntimeError('Missing uppercut launch/landing feedback')
    if (ground_contacts.count('rush')+ground_contacts.count('slam')+live_output.count('[MonsterRay] launch ') != output.count('[Game] cue=EnemyAttack ')
            or ground_contacts.count('hero-land') != 1 or ground_contacts.count('defeat') != 1
            or ground_contacts.count('stagger') != stagger_landings or ground_contacts.count('beam-brace') != beam_braces
            or len(ground_contacts) != live_output.count('[GroundImpact] sound=True')):
        raise RuntimeError('Ground contact event and audio were missing or duplicated')
    if args.slam and ground_contacts.count('slam') != 1:
        raise RuntimeError('Expected exactly one third-attack ground slam')
    if args.ray and (live_output.count('[MonsterRay] launch ') != 1 or live_output.count('[MonsterRayAudio] started') != 1 or live_output.count('[MonsterRayAudio] stopped') != 1 or '[BeamSurface] head-anchor=True vertices=3' not in output):
        raise RuntimeError('Missing head anchor, single ray or paired sound')
    if 'reaction=3.0' not in output:
        raise RuntimeError('Missing child reaction-time evidence')
    contacts={kind:live_output.count(f'[CombatAudio] contact={kind}') for kind in ('fist','heavy','beam')}
    entrance_steps=re.findall(r'\[MonsterEntrance\] step=(left|right) ',live_output)
    entrance_roars=live_output.count('[MonsterEntrance] roar sound=True')
    if entrance_steps!=['left','right'] or entrance_roars!=1 or ground_contacts.count('arrival')!=2:
        raise RuntimeError('Monster opening did not pair two landings with one roar')
    kick_contacts=live_output.count('[HeroKick] contact ')
    if kick_contacts!=(1 if args.finisher else 2) or live_output.count('[HeroKick] begin ')!=kick_contacts:
        raise RuntimeError('Combo kick did not match each accepted fifth/twenty-fifth hit once')
    expected_contacts = {'fist':12,'heavy':3,'beam':1} if args.finisher else {'fist':26,'heavy':6,'beam':2}
    if contacts != expected_contacts:
        raise RuntimeError(f'Contact sounds did not follow the accepted strikes: {contacts}')
    if 'effectDuck=0.42' not in live_output:
        raise RuntimeError('No speech-priority effect mix observed during playback')
    # These images come from this player run, not the independent Editor render
    # in cinematic-combat/frames. A unique directory prevents stale visual proof.
    required = ('battle-entry', 'monster-rush-left', 'monster-rush-right', 'guard-impact', 'hero-hurt',
                'hero-landed', 'hero-rising', 'hero-recovered',
                'beam-closeup-peak', 'beam-firing', 'beam-contact', 'beam-sustain', 'beam-fade', 'beam-braced', 'beam-pressure', 'beam-recovery', 'beam-reaction-entry', 'beam-reaction-peak', 'Paused', 'Victory',
                'victory-collapse', 'victory-turn', 'victory-dissolve', 'victory-motes', 'victory-hero',
                'transform-front', 'transform-radiance', 'transform-return', 'left-punch-recoil', 'right-punch-recoil',
                'combo-left', 'combo-right', 'monster-threat', 'monster-threat-return',
                'monster-stagger-lift', 'monster-stagger-land', 'monster-stagger-return', 'punch-impact-left', 'punch-impact-right',
                'combo-camera-peak', 'combo-camera-return', 'ground-rush', 'ground-hero-land', 'ground-stagger', 'ground-defeat',
                'uppercut-airborne', 'uppercut-land', 'uppercut-recover')
    required += ('monster-arms-left','monster-arms-right','monster-arms-uppercut','monster-arms-beam')
    required += ('monster-recovery-drop','monster-recovery-return')
    required += ('outpost-dust','outpost-settled')
    required += ('monster-entrance-step','monster-entrance-plant','monster-entrance-roar','monster-entrance-return')
    required += ('defeat-flash','defeat-billows','defeat-settling')
    required += ('kick-chamber','kick-contact','kick-retract','kick-setdown')
    if not args.finisher:
        required += ('kick-chamber-air','kick-contact-air','kick-retract-air','kick-setdown-air')
    if args.slam:
        required += ('slam-prepare','slam-swing','slam-ground','slam-wave','slam-rise')
    if args.ray:
        required += ('ray-prepare','ray-travel','ray-block','ray-fade','ray-recover')
    if args.linked:
        required += ('punch-link-prepare','punch-link-handoff')
    if args.finisher:
        # The defeated monster collapses after the release instead of taking a
        # recovery step back into battle. Ordinary rounds still require that shot.
        required = tuple(name for name in required if name != 'beam-recovery')
        required += ('final-strike-contact', 'final-strike-sustain', 'final-strike-release')
    else:
        required += ('final-punch-recovery',)
    final_contact = re.findall(r'\[FinalStrike\] contact action=(\w+) actionAge=([\d.]+) health=0', live_output)
    final_complete = re.findall(r'\[FinalStrike\] completed action=(\w+) actionAge=([\d.]+)', live_output)
    if len(final_contact) != 1 or len(final_complete) != 1 or final_contact[0][0] != final_complete[0][0]:
        raise RuntimeError('Final hit must complete exactly once with the same action')
    final_action, final_age = final_complete[0]
    if (args.finisher and final_action != 'Beam') or float(final_age) < (1.5 if args.finisher else .38):
        raise RuntimeError('Victory interrupted the last strike')
    for name in required:
        path = native / (name + '.png')
        if not path.is_file() or f'file={path}' not in output:
            raise RuntimeError(f'Missing current-player visual evidence: {name}')
    with Image.open(native / 'battle-entry.png') as frame:
        # The opaque health swatch must retain its authored display color
        # when the scene's lighting or postprocessing color space changes.
        # Search its normalized top-right HUD band instead of assuming a
        # single 1280x720 pixel; current audits run at 1080P as well.
        image=frame.convert('RGB');target=(255,161,59);best=None
        for y in range(round(frame.height*.035),round(frame.height*.115)):
            for x in range(round(frame.width*.70),round(frame.width*.96)):
                value=image.getpixel((x,y));distance=sum(abs(a-b) for a,b in zip(value,target))
                if best is None or distance<best[0]:best=(distance,value,x,y)
        if best is None or best[0]>6:
            raise RuntimeError(f'HUD color was encoded incorrectly: {best[1:] if best else None}')
        pixel=best[1]
    fps = [float(value) for value in re.findall(r'renderFps=(\d+\.\d+)', output)]
    result = {'result': 'passed', 'hero': args.hero, 'slam': args.slam, 'ray': args.ray, 'linked':args.linked, 'finisher':args.finisher, 'final_contact':final_contact[0], 'final_complete':final_complete[0], 'monster_rays': live_output.count('[MonsterRay] launch '), 'camera_used': False, 'wall_seconds': round(time.monotonic()-start, 2),
              'summary': match[0], 'fps_windows': fps,'requested_resolution':[args.width,args.height], 'stagger_landings':stagger_landings, 'combo_camera_shots':combo_shots, 'ground_contacts':ground_contacts,
              'launch_landings': launch_landings, 'started_utc': started.isoformat(), 'assembly_sha256': assembly_sha,
              'resources_sha256': resources_sha, 'hud_health_rgb': pixel, 'beam_volume_impacts': beam_impacts, 'beam_braces': beam_braces, 'contact_sounds':contacts,
              'evidence_directory': str(evidence), 'beam_reaction_cuts': reaction_cuts,
              'outpost_shocks': len(outpost_shocks), 'outpost_detachments_scheduled': detached_panels,
              'entrance_steps':entrance_steps, 'entrance_roars':entrance_roars,
              'screenshots': sorted(path.name for path in native.glob('*.png'))}
    (evidence / 'validation.json').write_text(json.dumps(result, indent=2))
    (root / 'artifacts/cinematic-combat/player-validation.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
