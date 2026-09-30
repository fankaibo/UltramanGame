"""Bind terminal-hit comparison to source captures and real player evidence."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/final-strike'
BASELINE = '0bc2fddb89a9ee7c9dd9d8f6fd1faebf2084a4ca'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check_sources(folder, before=False):
    result = {}
    for line in (folder / 'sources.txt').read_text().splitlines():
        name, expected = line.rsplit(' ', 1)
        relative = 'unity/Assets/' + name
        data = ((folder / 'review-source.cs').read_bytes() if name.startswith('Editor/') else
                subprocess.check_output(['git', 'show', BASELINE + ':' + relative], cwd=ROOT) if before else
                (ROOT / relative).read_bytes())
        if hashlib.sha256(data).hexdigest() != expected:
            raise RuntimeError('Capture no longer matches source: ' + relative)
        result[relative] = expected
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--punch-proof', type=Path, required=True)
    parser.add_argument('--beam-proof', type=Path, required=True)
    args = parser.parse_args()
    sources = {'before': check_sources(FOLDER / 'before/Tiga', True), 'after': {}}
    reports = {}
    for hero in ('Tiga', 'Mebius', 'Zero', 'Geed', 'Grigio'):
        folder = FOLDER / 'after' / hero
        sources['after'][hero] = check_sources(folder)
        reports[hero] = (folder / 'validation.txt').read_text()
        assert 'hits=1 releases=1 victory=1 landings=1 health=0 punches=15' in reports[hero]
        assert 'zeroDrift=0.000000' in reports[hero]
    for version in ('before', 'after'):
        assert len(list((FOLDER / version / 'Tiga/frames').glob('*.png'))) == 270
    pauses = (FOLDER / 'pause-validation.txt').read_text()
    assert len(pauses.splitlines()) == 3 and pauses.count('hits=1 releases=1 victories=1') == 3
    punch = json.loads(args.punch_proof.read_text())
    beam = json.loads(args.beam_proof.read_text())
    guided = json.loads((FOLDER / 'guided/guided-validation.json').read_text())
    build = json.loads((FOLDER / 'guided/build.json').read_text())
    assert punch['result'] == beam['result'] == guided['result'] == 'passed'
    assert not punch['finisher'] and beam['finisher']
    assert float(punch['final_complete'][1]) >= .38 and float(beam['final_complete'][1]) >= 1.5
    assert guided['play_again'] and guided['replay_battle_started'] and guided['gesture_wobble']['unwanted_attacks'] == 0
    app = ROOT / 'unity/Builds/TigaTraining.app/Contents/Resources/Data'
    for key, file in [('assembly_sha256', 'Managed/Assembly-CSharp.dll'), ('resources_sha256', 'resources.assets')]:
        actual = sha(app / file)
        assert all(item[key] == actual for item in (punch, beam, build['before'], build['after']))
    for name in ('final-strike-contact', 'final-strike-sustain', 'final-strike-release', 'victory-collapse', 'victory-hero'):
        shutil.copyfile(Path(beam['evidence_directory']) / 'native' / (name + '.png'), FOLDER / (name + '.png'))
    shutil.copyfile(Path(punch['evidence_directory']) / 'native/final-punch-recovery.png', FOLDER / 'final-punch-recovery.png')
    subprocess.run(['/opt/homebrew/bin/ffmpeg', '-y', '-v', 'error', '-framerate', '30', '-i', str(FOLDER / 'before/Tiga/frames/%04d.png'),
                    '-framerate', '30', '-i', str(FOLDER / 'after/Tiga/frames/%04d.png'), '-filter_complex', '[0:v][1:v]hstack=inputs=2[v]',
                    '-map', '[v]', '-frames:v', '270', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart',
                    str(FOLDER / 'comparison.mp4')], check=True)
    proof = dict(baseline=BASELINE, sources=sources, roster=reports, pauses=pauses, punch=punch, beam=beam, guided=guided, build=build,
                 media=dict(seconds=9, fps=30, kind='offline Unity capture, not runtime FPS'))
    (FOLDER / 'provenance.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    (FOLDER / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>最后一击完整释放</title><style>body{margin:0;background:#101a25;color:#edf4fa;font:16px/1.8 system-ui}main{max-width:1500px;margin:auto;padding:28px}p{color:#c2d1df;max-width:1100px}video,img{width:100%;border-radius:8px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px}a{color:#85ddec}</style>
<main><h1>光线释放完，再迎接胜利</h1><p>原版在怪兽血量归零的瞬间切入胜利，最后一次大招在动作约 0.45 秒就消失。现在保留完整的 1.5 秒放射与收束，然后衔接哥尔赞失衡、倒地和英雄庆祝。普通拳击也先完成收势；伤害仍只计算一次，怪兽归零后不能再攻击。</p>
<div class="pair"><span>修改前：命中便切走</span><span>当前：完整放射、收束、倒地</span></div>
<video controls loop playsinline src="comparison.mp4"></video><p>两侧从相同输入开始，9 秒无声对照；离线 30 FPS 不代表运行帧率。旧版更早进入胜利，时间差来自此次修正。</p>
<h2>实际安装包</h2><div class="pair"><img src="final-strike-contact.png" alt="最后一次命中"><img src="final-strike-sustain.png" alt="零血量时仍完整释放光线"></div>
<div class="pair"><img src="final-strike-release.png" alt="光线收束"><img src="victory-collapse.png" alt="随后倒地"></div>
<div class="pair"><img src="final-punch-recovery.png" alt="普通拳击完成收势"><img src="victory-hero.png" alt="英雄庆祝"></div>
<p>五位英雄的致命光线、怪兽落地与胜利衔接通过检查；实际程序分别完成普攻收尾和光线收尾。合成体感回放覆盖防御／大招抖动、自动合照、重拍、断流恢复及举手续局。没有使用真人摄像头或上传真人照片，合成输入不能代替孩子试玩。</p>
<p>参考街机的整体场景质感和动作丰富度仍需继续提升。<a href="provenance.json">源码与当前构建验证</a> · <a href="guided/guided-validation.json">合照续局验证</a></p></main></html>''')
    print(FOLDER / 'index.html')


if __name__ == '__main__':
    main()
