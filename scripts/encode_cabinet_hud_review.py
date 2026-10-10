"""Bind the cabinet HUD comparison to real player captures and source hashes."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/cabinet-hud'
BASELINE = '4b2668acab99c48e887767b236c2c3760a2bc4cf'
SHOTS = {
    'battle': '普通出拳', 'monster-rush-left': '怪兽前冲与防御',
    'uppercut-airborne': '上挑击飞', 'beam-reaction-peak': '光线命中镜头',
    'beam-closeup-peak': '角色大招特写', 'hero-landed': '受击倒地',
    'battle-entry': '开战', 'Victory': '胜利',
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    before = json.loads((FOLDER / 'before/validation.json').read_text())
    if before['git_head'] != BASELINE or before['result'] != 'passed':
        raise RuntimeError('Missing verified baseline player')
    for path, expected in before['sources'].items():
        data = subprocess.check_output(['git', 'show', BASELINE + ':' + path], cwd=ROOT)
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Baseline source mismatch: ' + path)
        if path.endswith(('GameWorld.cs', 'RiggedActor.cs', 'Pose.cs')) and digest(ROOT / path) != expected:
            raise RuntimeError('HUD comparison also changed camera, animation or recognition')
    after = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    if after['result'] != 'passed' or not all(after[k] for k in ('slam', 'ray', 'linked')):
        raise RuntimeError('Incomplete current native round')
    if before['summary'] != after['summary']:
        # Wall-clock playback can advance the final age by one frame.
        import re
        normalize = lambda value: re.sub(r'age=\d+\.\d+', 'age=<frame>', value)
        if normalize(before['summary']) != normalize(after['summary']):
            raise RuntimeError('Playback combat outcome changed')
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    if guided['result'] != 'passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Guidance, photo replay or protected gesture flow failed')
    data = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, path in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if any(value != digest(data / path) for value in (after[key], build['before'][key], build['after'][key])):
            raise RuntimeError('Stale build evidence')
    capture = FOLDER / 'after'
    capture.mkdir(exist_ok=True)
    for name in SHOTS:
        old = Path(before['evidence_directory']) / 'native' / (name + '.png')
        if digest(old) != digest(FOLDER / 'before' / (name + '.png')):
            raise RuntimeError('Modified baseline screenshot')
        shutil.copyfile(Path(after['evidence_directory']) / 'native' / (name + '.png'), capture / (name + '.png'))
    after['sources'] = {path: digest(ROOT / path) for path in before['sources']}
    (capture / 'validation.json').write_text(json.dumps(after, indent=2) + '\n')
    proof = dict(baseline=BASELINE, before=before, after=after, guided=guided, build=build,
                 comparison='Same named playback events; wall-clock capture may differ by frames, not a pixel-locked render.')
    (FOLDER / 'validation.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    controls = ''.join(f'<option value="{name}">{label}</option>' for name, label in SHOTS.items())
    html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>街机画面 · 战斗信息收边</title><style>
*{box-sizing:border-box}body{margin:0;background:#101820;color:#edf3f7;font:16px/1.7 system-ui}main{max-width:1480px;margin:auto;padding:28px}p{max-width:1080px;color:#bbcbd8}button,select{font:inherit;padding:8px 14px;background:#243c50;color:#fff;border:1px solid #668194;border-radius:6px;margin:6px 8px 10px 0}.compare{position:relative;line-height:0}.compare img{width:100%;display:block}.top{position:absolute;inset:0;clip-path:inset(0 50% 0 0)}.divider{position:absolute;top:0;bottom:0;left:50%;border-left:2px solid #ffe497;pointer-events:none}.labels{display:flex;justify-content:space-between;background:#20313f;padding:8px 14px}input{width:100%;margin:14px 0}a{color:#83ddeb}.notes{font-size:14px}h1{font-size:28px}summary{cursor:pointer}
</style><main><h1>让角色和交手动作占据画面</h1>
<p>顶部把姓名、能量和血量收进一条区域；分数回到顶部中央。左侧只显示一个命中／连击计数，光线演出时收起头像、分数和普通提示，保留边缘细条。底部动作引导和取景窗口保留原来的可读尺寸。</p>
<label>战斗节点 <select id="scene">CONTROLS</select></label><button id="old">只看修改前</button><button id="new">只看当前</button><button id="split">左右对照</button>
<div class="labels"><span>修改前</span><span>当前</span></div>
<div class="compare"><img id="after" src="after/battle.png" alt="当前游戏界面"><img class="top" id="before" src="before/battle.png" alt="修改前游戏界面"><div class="divider" id="line"></div></div>
<input id="slider" type="range" min="0" max="100" value="50" aria-label="前后对照分界位置">
<p class="notes">图片都来自真实 macOS 游戏的完整回放。同名节点的抓取可能相差数帧，因此这里比较界面占用和角色遮挡，不将逐像素差异作为改进幅度。此次没有修改摄像机、角色动画、动作识别和游戏规则。</p>
<details><summary>检查范围与剩余差距</summary><p>新版完成普通攻防、砸地、怪兽光线、倒地起身、必杀和胜利；无键鼠流程完成自动合照、重拍、流中断恢复和举手续局。干扰姿势测试使用合成输入，不代替孩子的真实摄像头体验。模型细节、更多动画和合照融合仍需继续完善，整体目标未完成。</p><a href="validation.json">源码、安装包与完整回放记录</a></details>
<script>
const slider=document.getElementById('slider'),before=document.getElementById('before'),after=document.getElementById('after'),line=document.getElementById('line');
function position(v){slider.value=v;before.style.clipPath=`inset(0 ${100-v}% 0 0)`;line.style.left=v+'%';line.hidden=v==0||v==100}
slider.oninput=()=>position(slider.value);document.getElementById('old').onclick=()=>position(100);document.getElementById('new').onclick=()=>position(0);document.getElementById('split').onclick=()=>position(50);
document.getElementById('scene').onchange=e=>{before.src='before/'+e.target.value+'.png';after.src='after/'+e.target.value+'.png'};
</script></main></html>'''.replace('CONTROLS', controls)
    (FOLDER / 'index.html').write_text(html)
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
