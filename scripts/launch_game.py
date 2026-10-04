"""Launch the local game and its camera owner together; release camera on exit."""
import argparse
import os
import plistlib
import signal
import subprocess
import sys
import time
from contextlib import ExitStack
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "unity/Builds/TigaTraining.app"

# Keep every direct invocation on the same dependency set as the desktop
# launcher.  Without this guard, `python3 scripts/launch_game.py` can silently
# use the system interpreter, start Unity, and then fail the camera worker at
# `import mediapipe` even though the project environment is healthy.
PROJECT_PYTHON = ROOT / ".venv/bin/python"
if PROJECT_PYTHON.is_file():
    try:
        current_python = Path(sys.executable).resolve()
        project_python = PROJECT_PYTHON.resolve()
    except OSError:
        current_python = project_python = None
    if current_python is not None and current_python != project_python:
        os.execv(str(project_python), [str(project_python), str(Path(__file__).resolve()), *sys.argv[1:]])

sys.path.insert(0, str(ROOT))
from vision.supervision import CameraSession, stop_process


def main():
    parser = argparse.ArgumentParser(description="一起启动本机游戏与相机；关闭游戏即释放相机。")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--keyboard", action="store_true", help="仅启动键盘练习，不打开相机")
    mode.add_argument("--demo", action="store_true", help="合成动作测试，游戏明确显示测试标记，不打开相机")
    parser.add_argument("--music", type=Path, help="指定本机 MP3/WAV/OGG/AIFF 背景音乐")
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
    def interrupted(signum, frame):
        raise KeyboardInterrupt
    signals = (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)
    handlers = {sig: signal.signal(sig, interrupted) for sig in signals}
    resources = ExitStack()
    try:
        try:
            if not args.keyboard:
                log_path = logs / "camera-last.log"
                if log_path.exists():
                    log_path.replace(logs / "camera-previous.log")
                camera_log = resources.enter_context(log_path.open("ab", buffering=0))
                # `sys.executable` may resolve to uv's base interpreter on
                # macOS, which drops the virtualenv site-packages for the
                # worker. Keep the child on the same project environment.
                camera_command = [str(PROJECT_PYTHON if PROJECT_PYTHON.is_file() else sys.executable),
                    "-m", "vision", "--no-preview", "--game-preview", "--ready-json"]
                if args.demo:
                    camera_command.append("--demo")
                camera = resources.enter_context(CameraSession(ROOT, camera_log, command=camera_command,
                    environment=environment, report=lambda message: print(message, flush=True)))
                ports = camera.ports
            print("游戏正在启动。关闭游戏窗口会同时关闭本次相机服务。", flush=True)
            command = [str(executable), "-screen-fullscreen", "0" if args.keyboard or args.demo else "1",
                       "-logFile", str(logs / "game-last.log")]
            if args.music:
                command += ["--music", str(args.music.expanduser().resolve())]
            if args.keyboard:
                command.append("--keyboard")
            else:
                command += ["--pose-port",str(ports["pose_port"]),"--preview-port",str(ports["preview_port"]),"--photo-port",str(ports["photo_port"])]
            game = subprocess.Popen(command, cwd=ROOT, env=environment,
                                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
            while game.poll() is None:
                if camera is not None:
                    camera.tick()
                time.sleep(.2)
            return game.returncode
        except KeyboardInterrupt:
            return 0
        except OSError as exc:
            print("启动失败：" + str(exc), file=sys.stderr)
            return 1
    finally:
        # A second terminal signal must not interrupt camera cleanup halfway.
        for sig in signals:
            signal.signal(sig, signal.SIG_IGN)
        try:
            stop_process(game)
            resources.close()
        finally:
            for sig, handler in handlers.items():
                signal.signal(sig, handler)


if __name__ == "__main__":
    raise SystemExit(main())
