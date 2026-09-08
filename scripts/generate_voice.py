"""Install the bundled hero voice, or explicitly generate a macOS system voice."""
import argparse
import hashlib
import json
import shutil
import subprocess
import re
import tempfile
import wave
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

def validate_pack(pack):
    manifest = json.loads((pack / 'manifest.json').read_text(encoding='utf-8'))
    if manifest.get('lines') != LINES:
        raise ValueError('角色语音包台词与游戏台词不一致，请先更新语音包。')
    for key in LINES:
        source = pack / (key + '.wav')
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if digest != manifest['audio'][key]['sha256']:
            raise ValueError(f'角色语音文件校验失败：{key}')
        with wave.open(str(source), 'rb') as clip:
            duration = clip.getnframes() / clip.getframerate()
            if clip.getnchannels() != 1 or clip.getsampwidth() != 2 or not 0.1 < duration < 20:
                raise ValueError(f'角色语音格式或时长无效：{key}')
    return manifest['name']


def main():
    parser = argparse.ArgumentParser(description='安装热血青年英雄语音包；不需要联网或运行语音模型')
    parser.add_argument('--voice', help='可选：改用已安装的 macOS 系统声音，例如 Tingting')
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    root = project / 'unity/Assets/Resources/Voice'
    pack = project / 'voice-packs/anime-hero'
    commands = ('say', 'afinfo') if args.voice else ('afconvert', 'afinfo')
    if any(not shutil.which(command) for command in commands):
        raise SystemExit('此脚本需要 macOS 音频工具，请在 macOS 上准备 Unity 语音资源。')
    try:
        profile = args.voice or validate_pack(pack)
    except (OSError, ValueError, KeyError, wave.Error) as error:
        raise SystemExit(f'语音包检查失败，未覆盖现有语音：{error}') from error
    root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='voice-generation-', dir=root) as temporary:
        for key, text in LINES.items():
            output = Path(temporary) / (key + '.aiff')
            if args.voice:
                command = ['say', '-v', args.voice, '-r', '175', '-o', str(output), text]
            else:
                command = ['afconvert', '-f', 'AIFF', '-d', 'BEI16', str(pack / (key + '.wav')), str(output)]
            subprocess.run(command, check=True)
            info = subprocess.run(['afinfo', str(output)], check=True, capture_output=True, text=True).stdout
            size = re.search(r'audio bytes:\s*(\d+)', info)
            if size is None or int(size.group(1)) == 0:
                raise SystemExit(f'语音 {key} 没有有效音频，未覆盖现有语音。')
        # Only install after every clip passes. The optional original beam cry is separate.
        for key in LINES:
            (Path(temporary) / (key + '.aiff')).replace(root / (key + '.aiff'))
    print(f'已安装 {len(LINES)} 条「{profile}」引导语音。大招原声文件保持独立。')


if __name__ == '__main__':
    main()
