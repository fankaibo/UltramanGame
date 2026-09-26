"""Make a synchronized, offline animation comparison from HeroMotionReview frames."""
import argparse
import html
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('folder', type=Path)
    parser.add_argument('--after', default='after', help='validated candidate subfolder')
    args = parser.parse_args()
    folder = args.folder.resolve()
    versions = {'before': folder / 'before', 'after': folder / args.after}
    ffmpeg = shutil.which('ffmpeg')
    if not ffmpeg:
        parser.error('ffmpeg is required')
    for version, source in versions.items():
        if not (source / 'validation.txt').is_file():
            parser.error(f'{version} has no completed validation')
        for frame in range(120):
            if not (source / 'frames' / f'frame-{frame:04d}.png').is_file():
                parser.error(f'{version} is missing frame {frame}')
    movie = folder / 'hero-motion-comparison.mp4'
    subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y',
                    '-framerate', '30', '-i', str(versions['before'] / 'frames/frame-%04d.png'),
                    '-framerate', '30', '-i', str(versions['after'] / 'frames/frame-%04d.png'),
                    '-filter_complex', '[0:v][1:v]hstack=inputs=2,scale=1920:540[v]',
                    '-map', '[v]', '-frames:v', '120', '-c:v', 'libx264', '-crf', '19',
                    '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(movie)], check=True)
    reports = '\n'.join(f'{version}: {(source / "validation.txt").read_text()}'
                        for version, source in versions.items())
    (folder / 'review.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>迪迦连续动作对照</title>
<style>body{margin:0;background:#0c121c;color:#e6effa;font:16px/1.7 system-ui;padding:24px}
main{max-width:1500px;margin:auto}h1{font-size:28px}p{color:#afc0d4}video{width:100%;background:#000}
.labels{display:flex;justify-content:space-around;padding:12px;background:#192537}
button{color:#dff4ff;background:#233a50;border:1px solid #4d7595;border-radius:7px;padding:9px 18px;margin:8px}
pre{white-space:pre-wrap;font-size:13px;background:#131f2e;padding:16px}a{color:#6bdef5}</style>
<main><h1>迪迦：站姿与左右拳连续动作</h1>
<p>左侧为修改前，右侧为重新烘焙的动画。两侧使用相同四次出拳输入和镜头，包含近距离左右连打及回收。
这是无声的 4 秒动画素材对照，未加入命中闪光或命中停顿；30 fps 为离线输出帧率，不代表游戏运行帧率。</p>
<div class="labels"><b>修改前 · 对称站姿</b><b>修改后 · 前手刺拳 / 后手直拳</b></div>
<video id="movie" controls loop playsinline src="hero-motion-comparison.mp4"></video>
<div><button data-rate="1">正常速度</button><button data-rate="0.5">半速看衔接</button>
<button data-rate="0.25">四分之一速度</button><button id="restart">重新播放</button></div>
<p>检查重点：前后手轮廓、腰胸跟随、收拳轨迹和支撑脚。动作识别阈值未调整。
命中仍位于动作 0.12 秒，0.38 秒完成回收。新版迪迦向前跨步 1.25 场景单位，并同步调整屈膝和支撑脚曲线，让拳头接近怪兽胸前。
新动画目前接入迪迦，其他英雄沿用现有动作。</p>
<pre>''' + html.escape(reports) + '''</pre>
<p><a href="before/sources.txt">旧版素材与代码校验</a> · <a href="''' + html.escape(args.after, quote=True) + '''/sources.txt">新版素材与代码校验</a></p>
<script>const movie=document.getElementById('movie');document.querySelectorAll('[data-rate]').forEach(b=>
b.onclick=()=>{movie.playbackRate=Number(b.dataset.rate);movie.play()});
document.getElementById('restart').onclick=()=>{movie.currentTime=0;movie.play()};</script></main></html>''')
    print(movie)


if __name__ == '__main__':
    main()
