"""Bind the new monster attack comparison to source and real-player evidence."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
FOLDER=ROOT/'artifacts/monster-ray'
BASELINE='fb8799a39a5d05add062e8fc574e5b29685efb8e'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sources(version):
    result={}
    for line in (FOLDER/version/'source.txt').read_text().splitlines():
        if line.startswith('UTC:'):continue
        file,expected=line.rsplit(' ',1);path='unity/Assets/'+file
        data=subprocess.check_output(['git','show',BASELINE+':'+path],cwd=ROOT) if version=='before' else (ROOT/path).read_bytes()
        if hashlib.sha256(data).hexdigest()!=expected:raise RuntimeError('Render source mismatch: '+path)
        result[path]=expected
    if not result:raise RuntimeError('No render sources')
    return result


def main():
    proof=dict(baseline_commit=BASELINE,before=sources('before'),after=sources('after'))
    sequence=(FOLDER/'after/sequence.csv').read_bytes()
    if sequence!=(FOLDER/'before/sequence.csv').read_bytes():raise RuntimeError('Battle sequence changed')
    for version in ('before','after'):
        if len(list((FOLDER/version/'frames').glob('*.png')))!=159:raise RuntimeError('Incomplete attack render')
        if 'blocks=4 hurt=0 health=50 fourth-attack=passed' not in (FOLDER/version/'validation.txt').read_text():raise RuntimeError('Incomplete defensive round')
    checks=(FOLDER/'checks/validation.txt').read_text()
    if checks.count(' passed')!=29:raise RuntimeError('Missing ray scenario or hero checks')
    player=json.loads((ROOT/'artifacts/cinematic-combat/player-validation.json').read_text())
    guided=json.loads((FOLDER/'guided/guided-validation.json').read_text())
    build=json.loads((FOLDER/'guided/build.json').read_text())
    data=ROOT/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key,file in [('assembly_sha256','Managed/Assembly-CSharp.dll'),('resources_sha256','resources.assets')]:
        if any(v!=digest(data/file) for v in (player[key],build['before'][key],build['after'][key])):raise RuntimeError('Build changed between player checks')
    if player['result']!='passed' or not player['ray'] or player['monster_rays']!=1:raise RuntimeError('Fourth-attack player check missing')
    if guided['result']!='passed' or guided['automatic_photos']!=2 or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Guided flow regression')
    native=FOLDER/'native';native.mkdir(exist_ok=True)
    for stage in ('prepare','travel','block','fade','recover'):
        shutil.copyfile(Path(player['evidence_directory'])/'native'/f'ray-{stage}.png',native/f'{stage}.png')
    subprocess.run(['/opt/homebrew/bin/ffmpeg','-y','-v','error','-framerate','30','-i',str(FOLDER/'before/frames/%04d.png'),
                    '-framerate','30','-i',str(FOLDER/'after/frames/%04d.png'),'-filter_complex','[0:v][1:v]hstack=inputs=2[v]',
                    '-map','[v]','-frames:v','159','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(FOLDER/'comparison.mp4')],check=True)
    proof.update(sequence_sha256=hashlib.sha256(sequence).hexdigest(),checks=checks,player=player,guided=guided,build=build,
                 media=dict(frames=159,fps=30,seconds=5.3,kind='offline render, not measured game frame rate'))
    (FOLDER/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n')
    (FOLDER/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>哥尔赞 · 超音波光线</title><style>body{margin:0;background:#111621;color:#edf1fc;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#becadb}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;text-align:center;gap:12px}select{font:inherit;color:inherit;background:#2c3251;border:1px solid #6e7a9e;padding:8px;border-radius:6px}a{color:#98dfea}</style>
<main><h1>让哥尔赞有不同的进攻方式</h1><p>现在每六次攻击中的第四次使用头部光线，与左右挥爪和重踏交替。哥尔赞站稳蓄能，光线从实际额头网格飞向护盾，随后淡出收势。孩子继续使用同一个防御姿势；语音后的反应时间和一次伤害判定保持不变。</p>
<div class="pair"><span>此前第四次攻击：再次挥爪</span><span>现在：蓄能、光线、收势</span></div><video controls loop playsinline preload="metadata" src="comparison.mp4"></video><p>5.3 秒、30 FPS 无声离线渲染，双方使用同样的战斗时序与防御输入；视频编码速率不是游戏实测帧率。实际游戏包含随光线启停的原创能量音。</p>
<h2>本次打包版实战</h2><select id="stage"><option value="prepare">头部蓄能</option><option value="travel">光线飞行</option><option value="block" selected>护盾接触</option><option value="fade">淡出</option><option value="recover">收势</option></select><img id="native" alt="实际游戏远程攻击">
<h2>五位英雄的防御画面</h2><select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select><img id="roster" alt="英雄抵挡头部光线">
<p>验证覆盖晚举手、提前放手、未防御、蓄力中出拳、晚到语音延长预警、暂停和重开，以及两轮攻击中不重复发射。打包版还完成整局、自动合照、重拍与举手续局。测试使用合成姿态和人像，没有打开家庭摄像头或调用 AI。</p>
<p>能力设定参考<a href="https://tsuburaya-prod.com/encyclopedia/golza">圆谷官方哥尔赞资料</a>；本轮动画、紫色光线与音效由项目制作，没有提取影视片段。角色模型细节和整体连续动画仍未达到参考街机的最终品质。</p><p><a href="checks/validation.txt">场景检查</a> · <a href="guided/guided-validation.json">无键鼠流程</a> · <a href="provenance.json">源码与构建证据</a></p>
<script>const stage=document.querySelector('#stage'),hero=document.querySelector('#hero');function show(){document.querySelector('#native').src='native/'+stage.value+'.png';document.querySelector('#roster').src='checks/'+hero.value+'.png'}stage.onchange=hero.onchange=show;show();</script></main></html>''')
    print(FOLDER/'index.html')


if __name__=='__main__':main()
