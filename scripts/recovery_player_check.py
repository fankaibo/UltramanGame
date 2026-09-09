"""Crash a synthetic worker during the built Unity game and verify same-round recovery."""
import json
import os
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from vision.supervision import CameraSession, stop_process


def main():
    app = ROOT / "unity/Builds/TigaTraining.app"
    with (app / "Contents/Info.plist").open("rb") as info:
        executable = app / "Contents/MacOS" / plistlib.load(info)["CFBundleExecutable"]
    logs = ROOT / "logs"
    logs.mkdir(exist_ok=True)
    player_log = logs / "camera-recovery-player.log"
    player_log.write_text("")
    environment = dict(os.environ, MPLCONFIGDIR=str(ROOT / ".cache/matplotlib"))
    game = None
    with (logs / "camera-recovery-player-worker.log").open("ab", buffering=0) as log:
        with CameraSession(ROOT, log, demo=True, environment=environment) as session:
            command = [str(executable), "-batchmode", "-forceNoAudio", "-screen-fullscreen", "0",
                       "-screen-width", "1280", "-screen-height", "720", "-logFile", str(player_log)]
            for name, port in session.ports.items():
                command += ["--" + name.replace("_", "-"), str(port)]
            try:
                game = subprocess.Popen(command, cwd=ROOT, env=environment, start_new_session=True,
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                started = time.monotonic()
                kill_at = None
                killed = False
                loss = resume = None
                while time.monotonic() - started < 65:
                    session.tick()
                    if game.poll() is not None:
                        raise RuntimeError("Unity player exited before recovery: " + str(game.returncode))
                    output = player_log.read_text(errors="replace")
                    if not killed and kill_at is None and "[Game] cue=Punch" in output:
                        kill_at = time.monotonic() + .5  # Let the already-accepted punch land first.
                    if kill_at is not None and not killed and time.monotonic() >= kill_at:
                        session.process.kill()
                        killed = True
                        print("Injected native worker exit during an active round", flush=True)
                    if killed:
                        loss = re.search(r"\[Input\] tracking=False mode=synthetic health=(\S+) energy=(\S+)", output)
                        resume = re.search(r"\[Game\] cue=Resume phase=Battle health=(\S+) energy=(\S+)", output)
                        if loss and resume and "[Game] cue=Punch" in output[resume.end():]:
                            break
                    time.sleep(.1)
                if not loss or not resume or loss.groups() != resume.groups():
                    raise RuntimeError("Round progress did not survive the outage; inspect " + str(player_log))
                if "[Game] cue=Punch" not in output[resume.end():]:
                    raise RuntimeError("No attack was accepted after reconnection")
                if output.count("[Game] cue=Transform") != 1 or session.generation != 2:
                    raise RuntimeError("Expected one transformation and one worker replacement")
                print(json.dumps(dict(result="passed", synthetic=True, game_pid=game.pid,
                    worker_generations=session.generation, preserved_health=float(loss[1]),
                    preserved_energy=float(loss[2]), resumed_attacks=True,
                    seconds=round(time.monotonic()-started, 1)), ensure_ascii=False), flush=True)
            finally:
                stop_process(game)


if __name__ == "__main__":
    main()
