"""Build a local comparison from verified photo renders and native flow proof."""
import hashlib
import json
import subprocess
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    root = Path(__file__).resolve().parents[1]
    folder = root/'artifacts/photo-3d'
    before = json.loads((folder/'before/source.json').read_text())
    source = subprocess.check_output(['git', 'show', before['commit']+':'+before['source']], cwd=root)
    if hashlib.sha256(source).hexdigest() != before['sha256']:
        raise RuntimeError('Baseline source mismatch')
    sources = {}
    for line in (folder/'after/sources.txt').read_text().splitlines():
        path, expected = line.rsplit(' ', 1)
        if digest(root/'unity/Assets'/path) != expected:
            raise RuntimeError('Outdated photo render: '+path)
        sources[path] = expected
    checks = (folder/'after/validation.txt').read_text()
    if checks.count('person-only matte=passed') != 5 or checks.count('reflection restored') != 5:
        raise RuntimeError('Incomplete roster or environment checks')
    guided = json.loads((folder/'guided/guided-validation.json').read_text())
    build = json.loads((folder/'guided/build.json').read_text())
    app = root/'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        if build['before'][key] != digest(app/file) or build['after'][key] != digest(app/file):
            raise RuntimeError('Guided proof does not match current build')
    if guided['result'] != 'passed' or not guided['replay_battle_started'] or guided['gesture_wobble']['unwanted_attacks']:
        raise RuntimeError('Guided flow or protected pose regression')
    if guided['automatic_photos'] != 2 or guided['photo_preview_p99_error'] > 2:
        raise RuntimeError('Photo save/preview incomplete')
    for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'):
        for pose in ('full', 'half', 'plate'):
            if not (folder/f'after/{hero}-{pose}.png').is_file():
                raise RuntimeError('Missing photo image')
    proof = dict(baseline=before, sources=sources, checks=checks, guided=guided, build=build)
    (folder/'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2))
    (folder/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>合照中的游戏 3D 英雄</title><style>body{margin:0;background:#111822;color:#e6edf5;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:22px}p{max-width:1100px;color:#bccada}img{display:block;width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}figure{margin:0}figcaption{padding:7px}select,button{font:inherit;background:#294356;color:inherit;padding:8px 14px;border:1px solid #5f7e91;border-radius:6px;margin:6px}a{color:#76d8e9}@media(max-width:760px){.pair{grid-template-columns:1fr}}</style>
<main><h1>把战斗中的英雄带进合照</h1><p>五位英雄现在使用游戏里的原模型和材质，固定在胜利姿势。英雄的尺寸与位置不会随取景或重拍变化，只缩放右侧拍照的人。示例均为合成测试图形，没有真人照片，也没有调用 AI。</p>
<h2>迪迦 · 旧图片与当前 3D 角色</h2><p>相同背景与测试人物像素。旧图片的绿色边缘和预制高光，改为模型的实际轮廓与场景照明。合照使用独立灯光，不改动返回战斗后的环境。</p><label>取景 <select id="framing"><option value="full-body">全身</option><option value="half-body">半身</option></select></label>
<div class="pair"><figure><figcaption>原来：独立预制图片</figcaption><img id="before" alt="旧合照"></figure><figure><figcaption>现在：游戏模型与材质</figcaption><img id="after" alt="新合照"></figure></div>
<h2>五位英雄 · 保留选择结果</h2><label>英雄 <select id="hero"><option value="Tiga">迪迦</option><option value="Mebius">梦比优斯</option><option value="Zero">赛罗</option><option value="Geed">捷德</option><option value="Grigio">格力乔</option></select></label><label>取景 <select id="body"><option value="full">全身人物</option><option value="half">半身人物</option><option value="plate">背景与英雄</option></select></label><img id="roster" alt="选中的游戏角色与测试人物">
<h2>本次打包版的合照预览</h2><p>真实 Unity 应用通过本机姿态协议完成整局、自动拍照两次、重拍、断流恢复和举手进入下一局；测试没有发送键鼠操作。预览与保存 PNG 的中心区域颜色检查通过。右侧 TEST 图形只是测试输入。</p><img src="guided/native/photo-Review.png" alt="打包版合照预览">
<p>本轮统一合照角色的造型与材质。真人的边缘、光照融合和裁断身体仍是后续工作；当前 AI 调色不等于生成式融合，整体街机品质也未宣称完成。</p><p><a href="after/validation.txt">五英雄检查</a> · <a href="guided/guided-validation.json">自动流程结果</a> · <a href="provenance.json">源码与构建证据</a></p>
<script>const framing=document.querySelector('#framing');function compare(){for(const v of ['before','after'])document.getElementById(v).src=v+'/'+framing.value+'.png'}framing.onchange=compare;compare();const hero=document.querySelector('#hero'),body=document.querySelector('#body');function roster(){document.querySelector('#roster').src='after/'+hero.value+'-'+body.value+'.png'}hero.onchange=body.onchange=roster;roster();</script></main></html>''')
    print(folder/'index.html')


if __name__ == '__main__':
    main()
