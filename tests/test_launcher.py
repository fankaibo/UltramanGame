"""The real launcher owns and cleans up its children when the terminal closes."""
import json
import os
from pathlib import Path
import plistlib
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest

from vision.supervision import stop_process

ROOT = Path(__file__).resolve().parents[1]


@unittest.skipUnless(os.name == "posix", "macOS/Linux process lifecycle")
class LauncherTests(unittest.TestCase):
    def test_terminal_termination_reaps_game_and_camera_and_preserves_previous_log(self):
        self._launch_and_close(False)

    def test_explicit_candidate_owns_and_releases_its_camera(self):
        self._launch_and_close(True)

    def _launch_and_close(self, candidate):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "scripts").mkdir()
            shutil.copy2(ROOT / "scripts/launch_game.py", root / "scripts/launch_game.py")
            app = root / ("candidate with spaces.app" if candidate else "unity/Builds/TigaTraining.app")
            contents = app / "Contents"
            (contents / "MacOS").mkdir(parents=True)
            with (contents / "Info.plist").open("wb") as info:
                plistlib.dump({"CFBundleExecutable": "test-game"}, info)
            executable = contents / "MacOS/test-game"
            executable.write_text(f"#!{sys.executable}\nimport os,time\nfrom pathlib import Path\n"
                "Path('game.pid').write_text(str(os.getpid()))\ntime.sleep(60)\n")
            executable.chmod(0o755)
            logs = root / "logs"
            logs.mkdir()
            (logs / "camera-last.log").write_text("prior failure evidence\n")
            env = dict(os.environ, PYTHONPATH=str(ROOT))
            with (logs / "launcher.log").open("wb") as log:
                launcher = subprocess.Popen([sys.executable, str(root / "scripts/launch_game.py"), "--demo"] + (["--app", str(app)] if candidate else []),
                    cwd=root, env=env, stdout=log, stderr=log, start_new_session=True)
                children = []
                try:
                    # Freshly installed macOS native libraries took ~25 seconds
                    # on the first load. Follow the launcher's startup allowance.
                    end = time.monotonic()+50
                    while time.monotonic()<end:
                        if (root / "game.pid").exists() and (logs / "camera-last.log").exists():
                            events = []
                            for line in (logs / "camera-last.log").read_text(errors="replace").splitlines():
                                try: events.append(json.loads(line))
                                except ValueError: pass
                            starts = [e for e in events if isinstance(e, dict) and e.get("event") == "worker_start"]
                            if starts and any(e.get("event") == "frames_resumed" for e in events if isinstance(e, dict)):
                                children = [int((root / "game.pid").read_text()), starts[-1]["pid"]]
                                break
                        time.sleep(.05)
                    self.assertEqual(2, len(children), "launcher must start a game and a functioning synthetic worker")
                    launcher.send_signal(signal.SIGTERM)
                    self.assertEqual(0, launcher.wait(timeout=8))
                    for pid in children:
                        with self.assertRaises(ProcessLookupError): os.kill(pid, 0)
                    children = []
                    self.assertEqual("prior failure evidence\n", (logs / "camera-previous.log").read_text())
                finally:
                    stop_process(launcher)
                    # Only test-owned children, also cleaned up if an assertion failed.
                    for pid in children:
                        try: os.kill(pid, signal.SIGKILL)
                        except ProcessLookupError: pass
