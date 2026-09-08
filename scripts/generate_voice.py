"""Generate fixed guide lines locally using an installed macOS voice."""
import argparse
import shutil
import subprocess
from pathlib import Path

LINES={
    'transform':'光的力量，准备变身！',
    'battle':'挥动拳头，保护城市！',
    'warning':'怪兽要攻击了！双手放在胸前防御。',
    'block':'挡住了，做得好！',
    'recover':'没关系，恢复力量，再来一次！',
    'energy':'能量满了，摆出光线姿势！',
    'beam':'发射光线！',
    'victory':'城市安全了！谢谢你，光之英雄！',
}
parser=argparse.ArgumentParser(description='使用已安装的 macOS 系统声音生成本地引导台词')
parser.add_argument('--voice',default='Tingting')
args=parser.parse_args()
if not shutil.which('say'):raise SystemExit('此脚本需要 macOS 的 say 命令。其他系统可在 Resources/Voice 中放入对应台词录音。')
root=Path(__file__).resolve().parents[1]/'unity/Assets/Resources/Voice'
root.mkdir(parents=True,exist_ok=True)
for key,text in LINES.items():
    subprocess.run(['say','-v',args.voice,'-r','155','-o',str(root/(key+'.aiff')),text],check=True)
print(f'生成 {len(LINES)} 条引导语音。音频保存在本机，不提交到 Git。')
