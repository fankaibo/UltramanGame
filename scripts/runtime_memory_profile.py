#!/usr/bin/env python3
"""Sample one or more isolated game runs without mixing in the parent process.

The command should be the executable that owns the Unity player process.  Use
``{cycle}`` and ``{output}`` in arguments when each run needs a separate proof
directory or log, for example::

    python3 scripts/runtime_memory_profile.py --cycles 3 \
      --command unity/Builds/TigaTraining.app/Contents/MacOS/TigaTraining \
      -logFile logs/memory-cycle-{cycle}.log

RSS and VSZ are read from macOS ``ps`` in KiB.  The summary deliberately keeps
the game's PID separate from any camera, AI, Unity Hub, or editor process.
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shlex
import subprocess
import sys
import time
from datetime import datetime, timezone


ROOT = Path(__file__).resolve().parents[1]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_process(pid: int) -> tuple[str, int | None, int | None]:
    """Return command, RSS KiB and VSZ KiB for *pid*.

    ``ps`` exits non-zero after a process has terminated.  A missing sample is
    recorded rather than inferred as zero, which keeps an exit from looking
    like a sudden memory release.
    """

    try:
        result = subprocess.run(
            ["ps", "-o", "rss=", "-o", "vsz=", "-o", "command=", "-p", str(pid)],
            check=False,
            capture_output=True,
            text=True,
        )
    except OSError:
        return "", None, None
    line = result.stdout.strip()
    if result.returncode != 0 or not line:
        return "", None, None
    fields = line.split(maxsplit=2)
    if len(fields) < 3:
        return "", None, None
    try:
        return fields[2], int(fields[0]), int(fields[1])
    except ValueError:
        return fields[2], None, None


def substitute(command: list[str], cycle: int, output: Path) -> list[str]:
    values = {"{cycle}": str(cycle), "{output}": str(output)}
    # Use literal replacement so unrelated braces in Unity arguments or paths
    # (for example a JSON fragment) do not turn into a formatting error.
    return [item.replace("{cycle}", values["{cycle}"]).replace("{output}", values["{output}"])
            for item in command]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--command", nargs=argparse.REMAINDER, required=True,
                        help="Unity player executable and arguments; {cycle}/{output} are replaced")
    parser.add_argument("--cycles", type=int, default=1,
                        help="number of fresh runs to launch sequentially (default: 1)")
    parser.add_argument("--interval", type=float, default=5.0,
                        help="seconds between RSS samples (default: 5)")
    parser.add_argument("--timeout", type=float, default=600.0,
                        help="maximum seconds per run; 0 disables the limit")
    parser.add_argument("--output", type=Path, default=ROOT / "logs/runtime-memory-profile.tsv",
                        help="TSV output path")
    parser.add_argument("--summary", type=Path,
                        help="optional JSON summary path (default: <TSV>.summary.json)")
    args = parser.parse_args()
    if args.cycles < 1 or args.interval <= 0 or args.timeout < 0:
        parser.error("cycles must be >= 1, interval must be > 0, and timeout must be >= 0")
    if not args.command:
        parser.error("--command needs an executable")

    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    summary_path = (args.summary or output.with_suffix(output.suffix + ".summary.json")).resolve()
    rows: list[dict[str, object]] = []
    cycles: list[dict[str, object]] = []
    with output.open("w", encoding="utf-8") as stream:
        stream.write("utc\tcycle\tseconds\tpid\trss_kib\tvsz_kib\tstatus\tcommand\n")
        for cycle in range(1, args.cycles + 1):
            command = substitute(args.command, cycle, output.parent / f"cycle-{cycle}")
            try:
                process = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.DEVNULL,
                                           stderr=subprocess.DEVNULL)
            except OSError as exc:
                print(f"cannot launch cycle {cycle}: {exc}", file=sys.stderr)
                return 2
            started = time.monotonic()
            values: list[int] = []
            while True:
                elapsed = time.monotonic() - started
                name, rss, vsz = read_process(process.pid)
                status = "running" if process.poll() is None else "exited"
                # A just-reaped macOS process can still appear in ``ps`` with
                # zero RSS.  Treat that terminal row as missing instead of
                # reporting a false memory release.
                if status == "exited":
                    name, rss, vsz = "", None, None
                row = {"utc": utc_now(), "cycle": cycle, "seconds": round(elapsed, 3),
                       "pid": process.pid, "rss_kib": rss, "vsz_kib": vsz,
                       "status": status, "command": name or shlex.join(command)}
                rows.append(row)
                if rss is not None:
                    values.append(rss)
                stream.write("\t".join("" if row[key] is None else str(row[key])
                                       for key in ("utc", "cycle", "seconds", "pid", "rss_kib", "vsz_kib", "status", "command")) + "\n")
                stream.flush()
                if status == "exited":
                    break
                if args.timeout and elapsed >= args.timeout:
                    process.terminate()
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
                    raise TimeoutError(f"cycle {cycle} exceeded {args.timeout:g}s")
                time.sleep(args.interval)
            code = process.returncode
            cycles.append({"cycle": cycle, "pid": process.pid, "returncode": code,
                           "samples": len(values), "first_rss_kib": values[0] if values else None,
                           "peak_rss_kib": max(values) if values else None,
                           "last_rss_kib": values[-1] if values else None,
                           "rss_samples_kib": values})
            if code != 0:
                print(f"cycle {cycle} exited with status {code}", file=sys.stderr)
                return 1
    summary = {"started_utc": rows[0]["utc"] if rows else utc_now(),
               "command_template": args.command, "cycles": cycles,
               "sample_count": len(rows), "host_pid": os.getpid()}
    summary_path.parent.mkdir(parents=True, exist_ok=True)
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False), flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except TimeoutError as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(124)
