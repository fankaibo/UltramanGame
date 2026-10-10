"""Present existing Unity recovery frames; do not synthesize gameplay evidence."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--runtime',default='runtime-anatomical-handoff')
    args=parser.parse_args();folder=ROOT/'artifacts/mocap-recovery-20261010'
    before=folder/'gameplay/before';after=folder/args.runtime/'Tiga-60'
    def anchor(path,key):
        rows=list(csv.DictReader(path.open()))
        # Match landing age, not arbitrary video start times.
        row=min((r for r in rows if r['action']=='Hurt'),key=lambda r:abs(float(r[key])-.30))
        return int(row['frame'])//2
    anchors=[anchor(before/'Tiga-sequence.csv','age'),anchor(after/'trace.csv','age')]
    sources=[sorted((p/'frames').glob('*.png')) for p in (before,after)]
    assert len(sources[0])==95 and len(sources[1])==120
    output=folder/'comparison';output.mkdir(exist_ok=True)
    font=ImageFont.truetype(str(ROOT/'unity/Assets/Resources/Fonts/NotoSansSC-Regular.otf'),30)
    small=ImageFont.truetype(str(ROOT/'unity/Assets/Resources/Fonts/NotoSansSC-Regular.otf'),19)
    for frame in range(140):
        canvas=Image.new('RGB',(1920,620),'#08111f');draw=ImageDraw.Draw(canvas)
        for i,files in enumerate(sources):
            index=min(len(files)-1,max(0,anchors[i]-10+frame))
            with Image.open(files[index]) as source:canvas.paste(source.resize((960,540)),(i*960,55))
        draw.text((25,10),'此前：程序起身 · 受击恢复 1.65 秒',font=font,fill='#bfcde1')
        draw.text((985,10),'现在：真人动作捕捉 · 受击恢复 2.55 秒',font=font,fill='#66dbed')
        draw.text((25,597),'Unity 离线画面，按落地时刻对齐；片尾不足部分定格。30 FPS 编码不代表游戏运行帧率。',font=small,fill='#bfcde1')
        canvas.save(output/f'{frame:04d}.png')
    video=folder/'recovery-comparison.mp4'
    subprocess.run(['ffmpeg','-y','-hide_banner','-loglevel','error','-framerate','30','-i',str(output/'%04d.png'),'-c:v','libx264','-preset','fast','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(video)],check=True)
    metrics={}
    for path in sorted((folder/args.runtime).glob('*/metrics.txt')):metrics[path.parent.name]=path.read_text()
    assert len(metrics)==15,'Expected all five heroes at three rates'
    identity={'status':'editor-validated-release-proof-pending','runtime':args.runtime,'landing_frames_30fps':anchors,'source_frame_counts':[len(p) for p in sources],'comparison_frames':140,'metrics':metrics,'hashes':{}}
    for path in [video,before/'Tiga-sequence.csv',after/'trace.csv',ROOT/'unity/Assets/Scripts/Runtime/CapturedRecoveryPose.cs',ROOT/'unity/Assets/Scripts/Runtime/RiggedActor.cs',ROOT/'unity/Assets/Scripts/Core/KnockdownMotion.cs']:
        identity['hashes'][str(path.relative_to(ROOT))]=sha(path)
    (folder/'runtime-identity.json').write_text(json.dumps(identity,ensure_ascii=False,indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh"><meta charset="utf-8"><title>真人起身动作对照</title><style>body{background:#08111f;color:#dce6f0;font:18px system-ui;max-width:1280px;margin:40px auto;padding:24px}video{width:100%}a{color:#66dbed}</style><h1>真人起身动作对照</h1><p>先收腿、转移重心，再站起回到自然待战姿势。包含五英雄三种采样率检查；发行包实际流程记录另见项目文档。</p><video src="recovery-comparison.mp4" controls loop muted></video><p>按落地时刻对齐，保留恢复时长差异；片尾定格。离线 30 FPS 编码不代表游戏帧率。</p><p><a href="runtime-identity.json">输入帧身份与全部检查指标</a></p></html>''')
    print(video)

if __name__=='__main__':main()
