"""Generate fixed guide lines locally using an installed macOS voice."""
import argparse
import shutil
import subprocess
import re
import tempfile
from pathlib import Path

LINES={
    'welcome':'小英雄，准备出发！把双手举高。',
    'transform':'光的力量，准备变身！',
    'battle':'挥动拳头，保护城市！',
    'warning':'小心！双手护住胸前！',
    'block':'挡住了，做得好！',
    'recover':'没关系，再来一次！',
    'energy':'能量满了！摆光线姿势，或者双手向前推！',
    'beam':'发射光线！',
    'victory':'城市安全了！谢谢你，光之英雄！',
    'resume':'准备好了，继续战斗！',
    'tutorial':'先收手，再向前挥拳。',
    'beam_help':'一只手抬高，另一只横在胸前。也可以双手向前推，停一下。',
}
parser=argparse.ArgumentParser(description='使用已安装的 macOS 系统声音生成本地引导台词')
parser.add_argument('--voice',default='Tingting')
args=parser.parse_args()
if not shutil.which('say'):raise SystemExit('此脚本需要 macOS 的 say 命令。其他系统可在 Resources/Voice 中放入对应台词录音。')
root=Path(__file__).resolve().parents[1]/'unity/Assets/Resources/Voice'
root.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(prefix='voice-generation-',dir=root) as temporary:
    for key,text in LINES.items():
        output=Path(temporary)/(key+'.aiff')
        subprocess.run(['say','-v',args.voice,'-r','175','-o',str(output),text],check=True)
        info=subprocess.run(['afinfo',str(output)],check=True,capture_output=True,text=True).stdout
        size=re.search(r'audio bytes:\s*(\d+)',info)
        if size is None or int(size.group(1))==0:
            raise SystemExit('系统语音未生成有效音频，请在允许访问 macOS 语音服务的终端重新运行。未覆盖现有的此条语音。')
        output.replace(root/(key+'.aiff'))
print(f'生成 {len(LINES)} 条引导语音。音频保存在本机，不提交到 Git。')
