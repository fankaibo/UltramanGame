"""Encode actual Unity fall frames; both sides begin at the same hit event."""
import hashlib
import json
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
folder=ROOT/'artifacts/mocap-fall-20261010'
for side in ('before','after'):
    assert len(list((folder/side/'Tiga-60/frames').glob('*.png')))==120
filters="[0:v]scale=960:540[a];[1:v]scale=960:540[b];[a][b]hstack"
video=folder/'fall-comparison.mp4'
subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-framerate','30','-i',str(folder/'before/Tiga-60/frames/%04d.png'),'-framerate','30','-i',str(folder/'after/Tiga-60/frames/%04d.png'),'-filter_complex',filters,'-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(video)],check=True)
probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-of','json',str(video)]))['streams'][0]
assert int(probe['nb_frames'])==120 and abs(float(probe['duration'])-4)<.01
report={'frames':120,'fps':30,'seconds':4,'sha256':hashlib.sha256(video.read_bytes()).hexdigest(),
        'metrics':{p.parent.name:p.read_text() for p in (folder/'after').glob('*/metrics.txt')}}
assert len(report['metrics'])==15
(folder/'video.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
(folder/'index.html').write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>真人失衡倒地对照</title><style>body{background:#08111f;color:#eef4ff;font:18px system-ui;margin:32px}video{width:100%;max-width:1920px}</style><h1>真人失衡倒地对照</h1><p>左：原程序倒地。右：动作捕捉的失去支撑、伸腿、后仰、落地，并衔接已有起身。受击恢复由 2.55 秒延长至 2.97 秒。</p><video src="fall-comparison.mp4" controls loop muted></video><p>同一受击起点，Unity 实际模型渲染；不是生成的概念视频。30 FPS 是编码速度，真实摄像头和电视仍待体验。</p></html>')
print(video)
