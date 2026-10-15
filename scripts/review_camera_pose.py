"""Local file -> production MediaPipe -> production C# gesture -> aligned review.

Never opens a camera or uploads media. Reports observations, not an accuracy score.
"""
import argparse
from collections import Counter
import hashlib
from functools import lru_cache
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from vision.protocol import FrameFactory

CONTEXTS = {
    'battle': (False, False, False),
    'charged': (True, False, False),
    'warning': (False, False, True),
    'charged-warning': (True, False, True),
    'waiting': (False, True, False),
}
IMAGE_SUFFIXES = {'.jpg', '.jpeg', '.png', '.webp', '.bmp'}


def digest(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def context_at(seconds, default, segments):
    for segment in segments:
        if segment['start'] <= seconds < segment['end']:
            return segment['context']
    return default


def read_segments(path):
    if path is None:
        return []
    segments = json.loads(path.read_text())
    if not isinstance(segments, list):
        raise ValueError('Context segments must be a list.')
    last = 0
    for row in segments:
        start, end = row['start'], row['end']
        if not (math.isfinite(start) and math.isfinite(end) and 0 <= last <= start < end):
            raise ValueError('Context segments must be finite, ordered and non-overlapping.')
        if row['context'] not in CONTEXTS:
            raise ValueError('Unknown context in segments.')
        last = end
    return segments


def joint_metrics(frame):
    """Report raw observations; confidence here does not override classifier gates."""
    if not frame['tracked']:
        return {'both_wrists_above_shoulders': None, 'wrists': {}}
    p = frame['points']
    span = max(.08, math.hypot(p[11]['x']-p[12]['x'], p[11]['y']-p[12]['y']))
    wrists = {}
    for name, hand, shoulder in (('L', 15, 11), ('R', 16, 12)):
        h, s = p[hand], p[shoulder]
        valid = all(v['visibility'] >= .55 and 0 <= v['x'] <= 1 and 0 <= v['y'] <= 1
                    for v in (h, s))
        wrists[name] = {'visibility': h['visibility'], 'shoulder_visibility': s['visibility'],
                        'reliable_at_055': valid, 'height_shoulder_widths': (s['y']-h['y'])/span,
                        'depth_shoulder_widths': (s['z']-h['z'])/span}
    reliable = all(w['reliable_at_055'] for w in wrists.values())
    return {'both_wrists_above_shoulders': all(w['height_shoulder_widths'] > 0 for w in wrists.values()) if reliable else None,
            'wrists': wrists}


def source_frames(path, start, seconds, input_mirrored=False):
    """Use decoded media timestamps. Never interpolate missed observations."""
    import cv2
    still = path.suffix.lower() in IMAGE_SUFFIXES
    if still:
        image = cv2.imread(str(path))
        if image is None:
            raise ValueError('Could not decode input image.')
        if input_mirrored:
            image = cv2.flip(image, 1)
        for i in range(math.ceil(seconds*30)):
            yield i, round(i*1000/30), image, 30., False
        return
    capture = cv2.VideoCapture(str(path))
    try:
        if not capture.isOpened():
            raise ValueError('Could not decode input video.')
        fps = capture.get(cv2.CAP_PROP_FPS)
        if not math.isfinite(fps) or not 1 <= fps <= 120:
            raise ValueError('Video needs a valid 1-120 FPS time base.')
        index, previous = 0, -1
        while True:
            ok, image = capture.read()
            if not ok:
                break
            raw_ms = capture.get(cv2.CAP_PROP_POS_MSEC)
            fallback = not math.isfinite(raw_ms) or raw_ms < 0 or round(raw_ms) <= previous
            stamp = round(index*1000/fps) if fallback else round(raw_ms)
            if stamp <= previous:
                raise ValueError('Non-monotonic media time; transcode to a known time base before review.')
            previous = stamp
            current = index
            index += 1
            if stamp < start*1000:
                continue
            if stamp >= (start+seconds)*1000:
                break
            if input_mirrored:
                image = cv2.flip(image, 1)
            yield current, stamp, image, fps, fallback
    finally:
        capture.release()


@lru_cache(maxsize=1)
def review_font():
    from PIL import ImageFont
    return ImageFont.truetype(str(ROOT/'unity/Assets/Resources/Fonts/NotoSansSC-Regular.otf'), 18)


def overlay(image, row, result, kind, last_event='暂无'):
    import cv2
    import numpy as np
    scale = min(960/image.shape[1], 720/image.shape[0], 1)
    w, h = max(2, int(image.shape[1]*scale)//2*2), max(2, int(image.shape[0]*scale)//2*2)
    # Match the game's mirrored preview; labels retain anatomical left/right.
    picture = cv2.flip(cv2.resize(image, (w, h)), 1)
    canvas = np.zeros((max(h, 400), w+420, 3), np.uint8)
    canvas[:h, :w] = picture
    frame = row['frame']
    if frame['tracked']:
        points = frame['points']
        def position(p):
            return (round((1-p['x'])*(w-1)), round(p['y']*(h-1)))
        for a, b in ((11, 12), (11, 13), (13, 15), (12, 14), (14, 16), (11, 23), (12, 24)):
            pa, pb = points[a], points[b]
            if all(0 <= p['x'] <= 1 and 0 <= p['y'] <= 1 for p in (pa, pb)):
                color = (50, 220, 90) if min(pa['visibility'], pb['visibility']) >= .55 else (0, 150, 255)
                cv2.line(canvas, position(pa), position(pb), color, 2)
        for index, label in ((15, 'L'), (16, 'R')):
            p = points[index]
            if 0 <= p['x'] <= 1 and 0 <= p['y'] <= 1:
                cv2.circle(canvas, position(p), 5, (255, 230, 30), -1)
                cv2.putText(canvas, label, position(p), cv2.FONT_HERSHEY_SIMPLEX, .7, (255, 255, 255), 2)
    value = result['input']
    words = {'LeftPunch':'左拳', 'RightPunch':'右拳', 'Beam':'大招', 'Transform':'开始'}
    events = '、'.join(words[k] for k in words if value[k]) or '无'
    owner = '大招' if value['BeamIntent'] else '防御' if value['GuardIntent'] else '无'
    yes = lambda v: '未知' if v is None else '是' if v else '否'
    lines = [f"源时间 {row['media_ms']/1000:.3f}s   {row['context']}", f"来源：{kind}",
             '镜像预览；L / R 是人物自身左右手', f"可追踪：{yes(value['Tracking'])}",
             f"动作占用：{owner}   护盾生效：{yes(value['Shield'])}", f"本帧事件：{events}",
             f"最近事件：{last_event}", f"大招持姿进度：{result['beam_progress']:.2f}",
             f"防御持姿进度：{result['shield_progress']:.2f}",
             f"大招需先释放：{yes(result['beam_needs_release'])}",
             f"双腕均高于肩膀：{yes(row['metrics']['both_wrists_above_shoulders'])}"]
    for side, wrist in row['metrics']['wrists'].items():
        lines.append(f"{side} 置信 {wrist['visibility']:.2f} 高 {wrist['height_shoulder_widths']:+.2f} 深 {wrist['depth_shoulder_widths']:+.2f}")
    lines += ['绿线：置信度 ≥ .55；橙线：低于 .55', '仅诊断观察，不表示识别准确率']
    from PIL import Image, ImageDraw
    annotated = Image.fromarray(cv2.cvtColor(canvas, cv2.COLOR_BGR2RGB))
    draw = ImageDraw.Draw(annotated)
    for i, text in enumerate(lines):
        draw.text((w+12, 6+i*26), text, font=review_font(), fill=(235, 235, 235))
    return cv2.cvtColor(np.asarray(annotated), cv2.COLOR_RGB2BGR)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='New directory under artifacts/ (media stays untracked)')
    parser.add_argument('--kind', choices=('camera-recording', 'reference-image', 'fixture'), required=True)
    parser.add_argument('--context', choices=tuple(CONTEXTS), required=True)
    parser.add_argument('--segments', type=Path, help='Optional JSON context timeline using absolute media seconds')
    parser.add_argument('--start', type=float, default=0)
    parser.add_argument('--seconds', type=float, default=20)
    parser.add_argument('--difficulty', type=int, choices=(0, 1, 2), default=0)
    parser.add_argument('--input-mirrored', action='store_true', help='Undo an already-mirrored recording before inference')
    args = parser.parse_args()
    args.input = args.input.resolve()
    folder = args.output.resolve()
    still = args.input.suffix.lower() in IMAGE_SUFFIXES
    if not args.input.is_file() or not math.isfinite(args.start) or args.start < 0 or not math.isfinite(args.seconds) or not 0 < args.seconds <= 120:
        parser.error('Existing input, non-negative start and 0 < seconds <= 120 are required.')
    if not folder.is_relative_to(ROOT/'artifacts') or folder.exists():
        parser.error('Output must be a new directory under this project artifacts/.')
    if still and (args.kind == 'camera-recording' or args.start != 0 or args.seconds > 5):
        parser.error('A still is not a camera recording; use start 0 and at most 5 seconds. It cannot validate a punch.')
    if not still and args.kind == 'reference-image':
        parser.error('reference-image requires an image file.')
    segments = read_segments(args.segments)
    input_sha = digest(args.input)
    folder.mkdir(parents=True)
    import cv2
    from vision.model import create_landmarker, camera_model_image, mp
    model_path = ROOT/'models/pose_landmarker_lite.task'
    env = dict(os.environ, DOTNET_CLI_HOME=str(ROOT/'.cache/dotnet'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
               DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    # Build before inference: replay must match current source, never a stale binary.
    with (folder/'core-build.log').open('w') as log:
        subprocess.run(['dotnet', 'build', 'tests/CoreChecks', '-c', 'Release', '--nologo'],
                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=120)
    factory = FrameFactory(source='synthetic' if args.kind == 'fixture' else 'camera')
    rows, inference_times = [], []
    with create_landmarker(model_path) as model, (folder/'poses.jsonl').open('w') as output:
        for index, stamp, image, fps, fallback in source_frames(args.input, args.start, args.seconds, args.input_mirrored):
            before = time.monotonic()
            prediction = model.detect_for_video(camera_model_image(image), stamp)
            inference_times.append((time.monotonic()-before)*1000)
            points = prediction.pose_landmarks[0] if prediction.pose_landmarks else None
            frame = factory.make(points, 100000+stamp)
            context = context_at(stamp/1000, args.context, segments)
            beam, transform, priority = CONTEXTS[context]
            row = dict(media_ms=stamp, source_index=index, frame=frame, context=context,
                       beam_available=beam, transform_available=transform, shield_priority=priority,
                       fps=fps, timestamp_fallback=fallback, metrics=joint_metrics(frame))
            rows.append(row)
            output.write(json.dumps(row, separators=(',', ':'), allow_nan=False)+'\n')
    if not rows:
        raise ValueError('No frames in the requested interval.')
    subprocess.run(['dotnet', str(ROOT/'tests/CoreChecks/bin/Release/net8.0/CoreChecks.dll'),
                    '--pose-replay', str(folder/'poses.jsonl'), str(folder/'decisions.jsonl'), str(args.difficulty)],
                   cwd=ROOT, env=env, check=True, timeout=120)
    decisions = [json.loads(line) for line in (folder/'decisions.jsonl').read_text().splitlines()]
    if len(rows) != len(decisions):
        raise ValueError('Decision/frame count mismatch.')
    writer = None
    last_event = '暂无'
    try:
        frames = source_frames(args.input, args.start, args.seconds, args.input_mirrored)
        rendered = 0
        for i, (index, stamp, image, fps, _) in enumerate(frames):
            row, decision = rows[i], decisions[i]
            if index != row['source_index'] or stamp != row['media_ms'] or stamp != decision['media_ms']:
                raise ValueError('Overlay does not align with classified frame.')
            names = {'LeftPunch':'左拳', 'RightPunch':'右拳', 'Beam':'大招', 'Transform':'开始'}
            fired = '、'.join(names[k] for k in names if decision['input'][k])
            if fired:
                last_event = f'{fired} @ {stamp/1000:.2f}s'
            kind = '重复静态图（不验证运动）' if still else '摄像头录像' if args.kind == 'camera-recording' else '生成测试素材'
            picture = overlay(image, row, decision, kind, last_event)
            if writer is None:
                writer = cv2.VideoWriter(str(folder/'review.mp4'), cv2.VideoWriter_fourcc(*'mp4v'), fps,
                                         (picture.shape[1], picture.shape[0]))
                if not writer.isOpened():
                    raise ValueError('Could not open review video writer.')
                if not cv2.imwrite(str(folder/'first-frame.jpg'), picture):
                    raise ValueError('Could not save diagnostic still.')
            writer.write(picture)
            rendered += 1
        if rendered != len(rows):
            raise ValueError('Overlay ended before all classified frames.')
    finally:
        if writer is not None:
            writer.release()
    events = Counter()
    shield_before = False
    for decision in decisions:
        value = decision['input']
        for key in ('LeftPunch', 'RightPunch', 'Beam', 'Transform'):
            events[key] += int(value[key])
        events['ShieldEntries'] += int(value['Shield'] and not shield_before)
        shield_before = value['Shield']
    if digest(args.input) != input_sha:
        raise ValueError('Input changed during review; discard this incomplete evidence.')
    report = dict(status='diagnostic-complete', accuracy_assessed=False, kind=args.kind, still_repeated=still,
                  limitations=['No manual ground truth: event counts are not accuracy.',
                               'File replay excludes live capture latency, Unity packet consumption and TV load.',
                               'Context is supplied explicitly; battle state and energy are not simulated.',
                               'Recognizer starts with no prior history; include preparation and retraction in the clip.',
                               'Repeated still frames cannot validate dynamic punches or real holding stability.',
                               'Review video uses source nominal FPS; JSON media timestamps are authoritative.'],
                  input_file=args.input.name, input_sha256=input_sha, model_sha256=digest(model_path),
                  mediapipe=mp.__version__, input_mirrored=args.input_mirrored, difficulty=args.difficulty,
                  context_default=args.context, segments=segments, frames=len(rows),
                  first_media_ms=rows[0]['media_ms'], last_media_ms=rows[-1]['media_ms'],
                  tracked_frames=sum(r['frame']['tracked'] for r in rows),
                  high_hand_frames=sum(r['metrics']['both_wrists_above_shoulders'] is True for r in rows),
                  uncertain_hand_frames=sum(r['metrics']['both_wrists_above_shoulders'] is None for r in rows),
                  timestamp_fallback_frames=sum(r['timestamp_fallback'] for r in rows), events=dict(events),
                  mean_inference_ms=sum(inference_times)/len(inference_times),
                  source_sha256={p:digest(ROOT/p) for p in ('vision/model.py', 'vision/__main__.py',
                      'unity/Assets/Scripts/Core/Pose.cs', 'unity/Assets/Scripts/Core/PunchMotion.cs',
                      'tests/CoreChecks/PoseReplayAudit.cs', 'scripts/review_camera_pose.py')})
    (folder/'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps({'report': str(folder/'report.json'), 'frames': len(rows), 'events': dict(events),
                      'accuracy_assessed': False}, ensure_ascii=False))


if __name__ == '__main__':
    main()
