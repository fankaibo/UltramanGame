"""Verify the volcanic particle comparison and bind it to the tested app."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/volcanic-ejecta'
BASELINE='d9e1f9b57dc426cb6fc1e7b6d39bac4132b73d9f'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    proof={'baseline':BASELINE,'sources':{}}
    for version in ('before','after'):
        capture=FOLDER/version
        if len(list((capture/'frames').glob('*.png')))!=240 or 'repeat=passed health=50 blocks=1 hurt=0' not in (capture/'validation.txt').read_text():
            raise RuntimeError('Incomplete stage sequence: '+version)
        sources={}
        for line in (capture/'sources.txt').read_text().splitlines():
            name,expected=line.rsplit(' ',1);relative='unity/Assets/'+name
            # The helper includes the new file list and sampler diagnostic in
            # both captures; all baseline runtime files come from the commit.
            data=(subprocess.check_output(['git','show',BASELINE+':'+relative],cwd=ROOT)
                  if version=='before' and name!='Editor/VolcanoStageReview.cs' else (ROOT/relative).read_bytes())
            if hashlib.sha256(data).hexdigest()!=expected:raise RuntimeError('Stale stage source: '+relative)
            sources[relative]=expected
        proof['sources'][version]=sources
    if (FOLDER/'before/sequence.csv').read_bytes()!=(FOLDER/'after/sequence.csv').read_bytes():
        raise RuntimeError('Stage upgrade changed fight timing')
    inspection=(FOLDER/'inspection/validation.txt').read_text()
    if not inspection.startswith('passed:'):raise RuntimeError('GPU flight/depth/clock validation missing')
    inspection_sources=json.loads((FOLDER/'inspection/sources.json').read_text())
    for path,expected in inspection_sources.items():
        if digest(ROOT/path)!=expected:raise RuntimeError('Stale GPU particle inspection')
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(h!=digest(data/file) for h in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Native evidence differs from current app')
    if (native['result']!='passed' or not all(native[k] for k in ('slam','ray','linked')) or
            guided['result']!='passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']):
        raise RuntimeError('Battle, gestures or photo flow failed')
    for name in ('battle','beam-reaction-peak','uppercut-airborne'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/(name+'.png'),FOLDER/(name+'.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error','-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
        '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),'-filter_complex','[0:v][1:v]hstack=inputs=2[v]',
        '-map','[v]','-frames:v','240','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof.update(gpu=inspection,gpu_sources=inspection_sources,native=native,guided=guided,build=build,
                 media={'seconds':8,'frames':240,'fps':30,'kind':'offline Unity render, not a measured game frame rate'})
    (FOLDER/'validation.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>火山喷发 · 碎块与火星</title><style>body{margin:0;background:#101721;color:#edf4fc;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{max-width:1100px;color:#b8cada}video,img{width:100%;display:block}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0}.labels{display:flex;justify-content:space-around;background:#213244;padding:8px}button{font:inherit;color:inherit;background:#26435c;border:1px solid #597991;border-radius:5px;padding:8px 16px;margin:8px}a{color:#83dceb}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<main><h1>喷出的熔岩会翻滚、冷却和回落</h1><p>原来的高亮小球改成不规则熔岩碎块：表面的岩壳与热裂口随飞行逐渐变暗，细小火星沿抛物线留下柔和短轨迹。每次喷出、回落和消散使用同一场景时钟，暂停时不会继续运动。</p>
<div class="labels"><b>修改前 · 发亮小球</b><b>当前 · 熔岩碎块与柔和火星</b></div><video src="comparison.mp4" controls loop playsinline></video><p>同一组镜头和输入，8 秒连续画面；480 条战斗状态完全一致。30 FPS 为导出帧率，不代表实际游戏帧率。</p>
<h2>同位置的喷口近景</h2><div class="pair"><figure><img src="before/vent-25.png" alt="旧版喷发"><figcaption>修改前</figcaption></figure><figure><img src="after/vent-25.png" alt="当前喷发"><figcaption>当前</figcaption></figure></div><button onclick="closeup.src='before/vent-25.png'">查看旧版</button><button onclick="closeup.src='after/vent-25.png'">查看当前</button><img id="closeup" src="after/vent-25.png">
<h2>实际 macOS 游戏</h2><img src="battle.png" alt="实际战斗"><div class="pair"><img src="beam-reaction-peak.png" alt="光线命中特写"><img src="uppercut-airborne.png" alt="上挑受击"></div>
<p>实际 GPU 检查包含动态变化、冻结及回退时钟、前景物体遮挡、飞行范围与地面接触。两组固定网格处理碎块与火星，不依赖独立的物理对象。当前安装包完成整局攻防、合照、重拍和举手续局；测试使用合成姿势与 TEST 人像，没有上传真人照片。</p>
<p>这仍是实时程序特效，未使用流体模拟或真实火山视频贴片。废墟几何、源模型细节和更丰富的连续演出仍需继续优化，整体目标尚未完成。</p><a href="validation.json">源码、GPU 检查和安装包验证</a></main></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':
    main()
