"""Launch the local game and its camera owner together; release camera on exit."""
import argparse
import os
import plistlib
import signal
import socket
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "unity/Builds/TigaTraining.app"


def stop(process):
    if process is None or process.poll() is not None:
        return
    process.send_signal(signal.SIGINT)
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.terminate()
        try:
            process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


def main():
    parser = argparse.ArgumentParser(description="一起启动本机游戏与相机；关闭游戏即释放相机。")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--keyboard", action="store_true", help="仅启动键盘练习，不打开相机")
    mode.add_argument("--demo", action="store_true", help="合成动作测试，游戏明确显示测试标记，不打开相机")
    args = parser.parse_args()
    if not APP.is_dir():
        print("尚未构建游戏。请先在 Unity 选择 UltramanGame → Build macOS Prototype。", file=sys.stderr)
        return 1
    with (APP / "Contents/Info.plist").open("rb") as info:
        executable = APP / "Contents/MacOS" / plistlib.load(info)["CFBundleExecutable"]
    logs = ROOT / "logs"
    logs.mkdir(exist_ok=True)
    camera = game = None
    environment = dict(os.environ, MPLCONFIGDIR=str(ROOT / ".cache/matplotlib"))
    with (logs / "camera-last.log").open("w") as camera_log:
        try:
            if not args.keyboard:
                # Do not take over another camera server or terminate somebody else's process.
                with socket.socket() as probe:
                    probe.settimeout(.3)
                    if probe.connect_ex(("127.0.0.1", 8765)) == 0:
                        print("动作服务已经在运行。请先关闭之前的游戏或相机服务，再双击启动。", file=sys.stderr)
                        return 1
                camera = subprocess.Popen([sys.executable, "-m", "vision", "--no-preview"] + (["--demo"] if args.demo else []),
                    cwd=ROOT, env=environment, stdout=camera_log, stderr=subprocess.STDOUT)
            print("游戏正在启动。关闭游戏窗口会同时关闭本次相机服务。", flush=True)
            command = [str(executable), "-screen-fullscreen", "0", "-screen-width", "1280",
                       "-screen-height", "720", "-logFile", str(logs / "game-last.log")]
            if args.keyboard:
                command.append("--keyboard")
            game = subprocess.Popen(command, cwd=ROOT, env=environment,
                                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            while game.poll() is None:
                if camera is not None and camera.poll() is not None:
                    print("相机服务已退出，请检查相机权限。详细信息：" + str(logs / "camera-last.log"), file=sys.stderr)
                    print("游戏仍可切换到键盘练习。关闭游戏窗口后可重新启动。", flush=True)
                    camera = None
                time.sleep(.2)
            return game.returncode
        except KeyboardInterrupt:
            return 0
        except OSError as exc:
            print("启动失败：" + str(exc), file=sys.stderr)
            return 1
        finally:
            stop(game)
            stop(camera)


if __name__ == "__main__":
    raise SystemExit(main())
