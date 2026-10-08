"""Export the actual Unity sequences with source and Release evidence checks."""
import hashlib
import json
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/battle-variety-20261008'
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    sources={}
    for row in (FOLDER/'render-source.txt').read_text().splitlines():
        name,expected=row.rsplit(' ',1)
        if sha(ROOT/'unity/Assets'/name)!=expected:
            raise RuntimeError('Stale rendered source: '+name)
        sources[name]=expected
    checks=(FOLDER/'validation.txt').read_text()
    for key in ['Tiga fifth strike','Mebius fifth strike','Zero fifth strike','Geed fifth strike','Grigio fifth strike',
                'Zero sluggers outbound=True return=True hits=1 launches=1',
                'rock-block held=True flight=True fragments=True hits=0 blocks=1',
                'rock-hurt held=True flight=True fragments=True hits=1 blocks=0']:
        if key not in checks:raise RuntimeError('Missing validation: '+key)
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,path in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        current=sha(data/path)
        if any(item[key]!=current for item in [native,build['before'],build['after']]):
            raise RuntimeError('Proof comes from a different Release: '+key)
    if native['result']!='passed' or guided['result']!='passed':raise RuntimeError('Player regression did not pass')
    parts=[('Mebius-melee',38),('Zero-sluggers',38),('rock-block',105),('rock-hurt',105)]
    for name,count in parts:
        if len(list((FOLDER/name).glob('[0-9][0-9][0-9][0-9].png')))!=count:raise RuntimeError('Missing footage '+name)
        subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error','-framerate','30','-i',str(FOLDER/name/'%04d.png'),
                        '-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/(name+'.mp4'))],check=True)
    command=['/opt/homebrew/bin/ffmpeg','-y','-v','error']
    for name,_ in parts:command+=['-i',str(FOLDER/(name+'.mp4'))]
    command+=['-filter_complex','[0:v][1:v][2:v][3:v]concat=n=4:v=1:a=0[v]','-map','[v]',
              '-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'battle-variety.mp4')]
    subprocess.run(command,check=True)
    report=dict(sources=sources,model_checks=checks.splitlines(),native=native,guided=guided,build=build,
                media=dict(frames=sum(p[1] for p in parts),export_fps=30,kind='offline Unity rendering; not a runtime FPS benchmark'))
    (FOLDER/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>远近战与英雄武器</title><style>body{max-width:1400px;margin:24px auto;padding:20px;background:#101722;color:#edf4ff;font:16px/1.7 system-ui}p{color:#b9c8da}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}video,img{width:100%}a{color:#84d9f2}@media(max-width:800px){.grid{grid-template-columns:1fr}}</style>
<h1>远近战与英雄武器</h1><p>保持45°对峙，站位距离增加约52%。向前挥拳远程出招；侧挥冲近攻击，速度继续跟随孩子出手快慢。</p>
<div class="grid"><section><h2>梦比优斯 · 左手侧挥光剑</h2><video src="Mebius-melee.mp4" controls autoplay muted loop></video></section>
<section><h2>赛罗 · 双头镖飞出并返回</h2><video src="Zero-sluggers.mp4" controls autoplay muted loop></video></section>
<section><h2>怪兽投石 · 护盾挡住并碎裂</h2><video src="rock-block.mp4" controls autoplay muted loop></video></section>
<section><h2>怪兽投石 · 未防御则受击倒地</h2><video src="rock-hurt.mp4" controls autoplay muted loop></video></section></div>
<p>上方是实际游戏代码与骨骼模型的连续离线渲染，用于看清动作；30 FPS导出不代表游戏性能。另已验证1080P发行包整局与合成体感自动合照续局。影视级模型、原声和真实儿童／电视效果仍待完善。</p>
<p>来源核对：<a href="https://toy.bandai.co.jp/ja/series/ultraman/item/01_4785/">赛罗头镖</a> · <a href="https://toy.bandai.co.jp/ja/topics/01_19804/">梦比姆光剑</a>。本轮武器采用游戏内程序模型和特效，未新增影视原声音频。</p><a href="validation.json">本次源码、模型与发行回放证据</a></html>''')
    print(FOLDER/'index.html')
if __name__=='__main__':main()
