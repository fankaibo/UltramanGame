"""Bind deterministic volcanic contact captures to the tested macOS app."""
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/volcanic-landing'
BASELINE = 'fee530126e23139d2526593f2325ef3ba62dff61'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def manifest(folder, before=False, helper=None):
    result = {}
    for line in (folder / 'sources.txt').read_text().splitlines():
        name, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/' + name
        data = (subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT)
                if before and name != helper else (ROOT / relative).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Capture has stale source: ' + relative)
        result[relative] = expected
    return result


def main():
    sources, contacts = {}, {}
    for version in ('before', 'after'):
        capture = FOLDER / version
        assert len(list((capture / 'frames').glob('*.png'))) == 240
        assert 'repeat=passed health=50 blocks=1 hurt=0' in (capture / 'validation.txt').read_text()
        sources[version] = manifest(capture, version == 'before')
        close = FOLDER / ('landing-' + version)
        assert len(list(close.glob('*.png'))) == 48
        contacts[version] = manifest(close, version == 'before', 'Editor/VolcanicEjectaReview.cs')
    expected_changes = {'unity/Assets/Scripts/Runtime/VolcanicEjecta.cs',
                        'unity/Assets/Resources/VolcanicBomb.shader'}
    assert {p for p in sources['before'] if sources['before'][p] != sources['after'][p]} == expected_changes
    assert {p for p in contacts['before'] if contacts['before'][p] != contacts['after'][p]} == expected_changes
    sequence = (FOLDER / 'before/sequence.csv').read_bytes()
    assert sequence == (FOLDER / 'after/sequence.csv').read_bytes()
    checks = (FOLDER / 'inspection/validation.txt').read_text()
    assert checks.startswith('passed:') and 'contactCases=27' in checks
    native = json.loads((ROOT / 'artifacts/cinematic-combat/player-validation.json').read_text())
    assert native['result'] == 'passed' and all(native[k] for k in ('slam', 'ray', 'linked'))
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        assert native[key] == digest(app / file)
    for name in ('battle', 'slam-swing', 'beam-reaction-peak'):
        shutil.copyfile(Path(native['evidence_directory']) / 'native' / (name + '.png'), FOLDER / (name + '.png'))
    for before, after, count, name in [('before/frames/%04d.png', 'after/frames/%04d.png', 240, 'comparison'),
                                      ('landing-before/%04d.png', 'landing-after/%04d.png', 48, 'landing')]:
        subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / before),
                        '-framerate', '30', '-i', str(FOLDER / after), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                        '-map', '[v]', '-frames:v', str(count), '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                        '-movflags', '+faststart', str(FOLDER / (name + '.mp4'))], check=True)
    proof = dict(baseline=BASELINE, stage_sources=sources, contact_sources=contacts, gpu=checks, native=native,
                 sequence_sha256=hashlib.sha256(sequence).hexdigest(),
                 media=dict(stage_seconds=8, contact_seconds=1.6, fps=30, kind='offline Unity render, not runtime FPS'),
                 scope='Visual eruption change. Photo and gesture logic unchanged; no new AI upload or photo-flow run.')
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>火山碎块的落地与冷却</title><style>body{margin:0;background:#111923;color:#edf3f8;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#bfcedb;max-width:1150px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}button{font:inherit;padding:8px 18px;margin:6px;background:#294256;color:inherit;border:1px solid #54738a}a{color:#86d9ed}</style>
<main><h1>让火山碎块真正落到岩地上</h1><p>原版碎块接近地面时缩小消失。新版保持实体大小接地，短促弹跳、翻滚后停留冷却，留下接触阴影，最后逐渐消散。地形的坡度与岩块的旋转都参与接触计算。</p>
<div class="pair"><span>修改前</span><span>当前</span></div><video controls loop playsinline src="landing.mp4"></video><p>同一块运行时碎块的近景，分别对齐各自第一次接地时刻。临时相机与灰色地面用于看清落点；未替换正式游戏背景。近景没有显示其他碎块与光晕。</p>
<h2>完整战斗场景</h2><video controls loop playsinline src="comparison.mp4"></video><p>同一组输入与镜头的 8 秒对照，480 条战斗状态逐字一致。30 FPS 是离线视频导出帧率，不是游戏实时帧率。</p>
<button onclick="vent.src='before/vent-25.png'">修改前</button><button onclick="vent.src='after/vent-25.png'">当前</button><img id="vent" src="after/vent-25.png" alt="同位置喷口近景">
<h2>实际安装包</h2><img src="battle.png" alt="实际游戏战斗"><div class="pair"><img src="slam-swing.png" alt="怪兽砸地"><img src="beam-reaction-peak.png" alt="必杀命中"></div>
<p>平地和正反斜面在 15／30／60 Hz 下的 27 组落地检查通过；GPU 检查覆盖冻结、回退时钟和前景遮挡。当前安装包完成整局攻防、砸地、远程光线、暂停恢复与胜利。本轮没有修改姿势识别或合照代码，也没有再次上传照片或调用 AI。</p>
<p>这仍是程序化实时喷发，整体角色细节、动画丰富度与参考街机仍有差距。<a href="provenance.json">源码、落地与实机验证记录</a></p></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
