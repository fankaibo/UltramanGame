"""Publish a local comparison bound to the rendered sources and tested player."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/scanned-outcrops'
BASELINE='bc16278e60d0a796e5dc34260ca4171fff2b5701'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    proof=dict(baseline=BASELINE,sources={},offline_export_fps=30)
    for side in ('before','after'):
        folder=FOLDER/side
        if len(list((folder/'frames').glob('*.png')))!=240:raise RuntimeError('Incomplete capture: '+side)
        if 'repeat=passed health=50 blocks=1 hurt=0' not in (folder/'validation.txt').read_text():
            raise RuntimeError('Invalid stage sequence')
        sources={}
        for line in (folder/'sources.txt').read_text().splitlines():
            path,expected=line.rsplit(' ',1);relative='unity/Assets/'+path
            data=(ROOT/relative).read_bytes() if side=='after' else subprocess.check_output(['git','show',BASELINE+':'+relative],cwd=ROOT)
            if hashlib.sha256(data).hexdigest()!=expected:raise RuntimeError('Stale render: '+relative)
            sources[relative]=expected
        proof['sources'][side]=sources
    if (FOLDER/'before/sequence.csv').read_bytes()!=(FOLDER/'after/sequence.csv').read_bytes():
        raise RuntimeError('Battle states changed between renders')
    assets=ROOT/'unity/Assets/Resources/Environment/ScannedRocks'
    manifest=json.loads((assets/'sources.json').read_text())
    for item in manifest['files']:
        if sha(assets/item['file'])!=item['sha256']:raise RuntimeError('Changed source asset')
    flow=(FOLDER/'finisher/after/flow-validation.txt').read_text()
    for hero in ('Tiga','Mebius','Zero','Geed','Grigio'):
        if hero+' 60fps flight=' not in flow:raise RuntimeError('Missing hero finisher check')
    if flow.count('pause-and-reset=passed')!=3:raise RuntimeError('Missing interruption checks')
    native=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    app=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(value!=sha(app/file) for value in (native[key],build['before'][key],build['after'][key])):
            raise RuntimeError('Native evidence differs from delivered app')
    if native['result']!='passed' or not all(native[k] for k in ('slam','ray','linked')):
        raise RuntimeError('Incomplete native battle')
    if guided['result']!='passed' or guided['gesture_wobble']['unwanted_attacks'] or not guided['replay_battle_started']:
        raise RuntimeError('Guided regression')
    (FOLDER/'native').mkdir(exist_ok=True)
    for name in ('battle','guard-impact','beam-reaction-peak','victory-hero'):
        shutil.copyfile(Path(native['evidence_directory'])/'native'/(name+'.png'),FOLDER/'native'/(name+'.png'))
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error',
        '-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
        '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),
        '-filter_complex','[0:v][1:v]hstack=inputs=2[v]','-map','[v]',
        '-frames:v','240','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof.update(assets=manifest,finisher=flow,native=native,guided=guided,build=build,
                 comparison=dict(seconds=8,frames=240,state_samples=480))
    (FOLDER/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>火山战场 · 扫描岩块</title><style>body{margin:0;background:#111821;color:#eef4fa;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bacbd8;max-width:1100px}video,img{width:100%;border-radius:6px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px;text-align:center}button{font:inherit;background:#244959;color:white;border:1px solid #71929e;padding:8px 20px;margin:8px}a{color:#86d4ec}.zoom{overflow:hidden;aspect-ratio:3.4/1;border-radius:6px}.zoom img{width:240%;max-width:none;transform:translate(-2%,-46%)}h1{font-size:28px}h2{font-size:21px}</style>
<main><h1>让战场岩块具有真实的断面与轮廓</h1><p>13 处主要岩块换成两款扫描模型，使用各自原有的颜色、法线和粗糙度贴图，并调整为火山灰场地的冷灰色。原先较圆滑、重复的外轮廓得到改善。此轮集中在环境几何，不代表角色动画与整体街机演出已经达到目标。</p>
<div class="pair"><b>修改前</b><b>扫描岩块</b></div><video src="comparison.mp4" controls loop playsinline></video><p>同一段 8 秒攻防、相同镜头和输入，480 条战斗状态一致。30 FPS 是离线视频导出设置，不是游戏实测帧率。</p>
<h2>同位置切换</h2><button onclick="setSide('before')">修改前</button><button onclick="setSide('after')">当前</button><img id="scene" src="after/battle-420.png" alt="完整战斗构图">
<h2>左侧岩块细节</h2><div class="zoom"><img id="detail" src="after/battle-420.png" alt="同一张渲染图的局部放大"></div>
<h2>打包版实际画面</h2><img src="native/battle.png" alt="实际战斗"><div class="pair"><img src="native/guard-impact.png" alt="实际格挡"><img src="native/beam-reaction-peak.png" alt="大招反应镜头"></div><img src="native/victory-hero.png" alt="胜利镜头">
<p>实际程序完成整局攻防及自动合照、重拍、断流恢复和举手续局。测试使用合成输入，不代表真人体感已重新验收，也没有调用家庭照片 AI 美化。</p>
<p>模型由 Jenelle van Heerden 制作，来源为 <a href="https://polyhaven.com/a/rock_07">Poly Haven / Rock 07</a> 和 <a href="https://polyhaven.com/a/rock_09">Rock 09</a>，按 <a href="https://polyhaven.com/license">CC0</a> 使用。它们是风化石块扫描，在本场景中作色彩适配，不是富士山实地扫描。</p>
<p><a href="provenance.json">素材、渲染与当前安装包核对记录</a> · <a href="guided/guided-validation.json">无键鼠回放</a></p></main><script>function setSide(s){document.getElementById('scene').src=document.getElementById('detail').src=s+'/battle-420.png'}</script></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':main()
