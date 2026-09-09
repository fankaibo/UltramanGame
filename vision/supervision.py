"""Bounded recovery of the native camera worker; one game, stable loopback sockets."""
from collections import deque
import json
import signal
import socket
import subprocess
import sys
import threading
import time


def stop_process(process, interrupt_timeout=2, terminate_timeout=1):
    """Reap our child before replacing it, including a stuck native inference call."""
    if process is None:
        return
    for sig, timeout in ((signal.SIGINT, interrupt_timeout), (signal.SIGTERM, terminate_timeout),
                         (signal.SIGKILL, None)):
        if process.poll() is not None:
            return
        try:
            process.send_signal(sig)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=timeout)
            return
        except subprocess.TimeoutExpired:
            continue


class RetryBudget:
    """Bound both rapid flapping and consecutive failures of slow startup."""
    def __init__(self, delays=(1, 2, 4), window=120):
        self.delays, self.window = delays, window
        self.failures = deque()
        self.consecutive = 0
        self.healthy_since = None

    def healthy(self, now):
        if self.healthy_since is None:
            self.healthy_since = now
        if now - self.healthy_since >= 60:
            self.consecutive = 0

    def failed(self, now):
        while self.failures and now - self.failures[0] >= self.window:
            self.failures.popleft()
        self.failures.append(now)
        self.consecutive += 1
        self.healthy_since = None
        attempt = max(self.consecutive, len(self.failures))
        return self.delays[attempt-1] if attempt <= len(self.delays) else None


class _Heartbeat:
    def __init__(self):
        self.lock = threading.Lock()
        self.at = None
        self.sequence = 0

    def receive(self, message):
        if not isinstance(message, dict) or message.get("event") != "frame":
            return
        sequence = message.get("sequence")
        with self.lock:
            if type(sequence) is int and sequence > self.sequence:
                self.sequence, self.at = sequence, time.monotonic()

    def time(self):
        with self.lock:
            return self.at


class CameraSession:
    """The parent holds listening sockets but never captures or queues any images.

    Only the worker inherits these three descriptors. Each replacement receives
    the same sockets after its predecessor has been reaped. Unity reconnects and
    a new streamId resets gesture history without resetting battle progress.
    """
    def __init__(self, root, log, *, demo=False, environment=None, report=print,
                 command=None, startup_timeout=45, stall_timeout=6, retries=None):
        self.root, self.log, self.environment, self.report = root, log, environment, report
        self.command = command if command is not None else [sys.executable, "-m", "vision",
            "--no-preview", "--game-preview", "--ready-json"] + (["--demo"] if demo else [])
        self.startup_timeout, self.stall_timeout = startup_timeout, stall_timeout
        self.retries = retries if retries is not None else RetryBudget()
        self.listeners = []
        self.process = self.reader = None
        self.ports = {}
        self.generation = 0
        self.state = "closed"
        self.retry_at = 0
        self.heartbeat = None

    def _log(self, event, **details):
        self.log.write((json.dumps(dict(event=event, generation=self.generation,
            time=time.time(), **details), ensure_ascii=False) + "\n").encode())

    def __enter__(self):
        try:
            for name in ("pose", "preview", "photo"):
                listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.listeners.append(listener)
                listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                listener.bind(("127.0.0.1", 0))
                listener.listen(16)
                self.ports[name + "_port"] = listener.getsockname()[1]
            self.state = "starting"
            self._spawn()
            return self
        except BaseException:
            self.close()
            raise

    def _spawn(self):
        self.generation += 1
        self.heartbeat = _Heartbeat()
        self.started_at = time.monotonic()
        self.state = "starting"
        command = list(self.command)
        for name, listener in zip(("pose", "preview", "photo"), self.listeners):
            command += ["--" + name + "-fd", str(listener.fileno())]
        try:
            self.process = subprocess.Popen(command, cwd=self.root, env=self.environment,
                pass_fds=tuple(s.fileno() for s in self.listeners), stdout=subprocess.PIPE,
                stderr=self.log, start_new_session=True)
        except OSError as exc:
            self._failed("spawn: " + str(exc))
            return
        output, heartbeat = self.process.stdout, self.heartbeat
        # stderr appends directly to the same unbuffered O_APPEND log. No pixels,
        # landmarks or audio are logged; only lifecycle and frame-counter messages.
        def read_output():
            try:
                for line in output:
                    self.log.write(line)
                    try:
                        heartbeat.receive(json.loads(line))
                    except (ValueError, UnicodeError):
                        pass
            finally:
                output.close()
        self.reader = threading.Thread(target=read_output, daemon=True, name="Camera heartbeat")
        self.reader.start()
        self._log("worker_start", pid=self.process.pid, ports=self.ports)

    def _reap(self):
        stop_process(self.process)
        if self.reader:
            self.reader.join()
        self.process = self.reader = None

    def _failed(self, reason):
        self._log("worker_failure", reason=reason)
        self._reap()
        delay = self.retries.failed(time.monotonic())
        if delay is None:
            self.state = "failed"
            self.report("相机服务连续恢复失败，本次停止自动重试；游戏进度保留，可切换键盘练习。详情见 logs/camera-last.log。")
        else:
            self.state = "retrying"
            self.retry_at = time.monotonic() + delay
            self.report(f"相机服务中断，{delay:g} 秒后自动重连；当前游戏进度保留。")

    def tick(self):
        if self.state in ("closed", "failed"):
            return
        now = time.monotonic()
        if self.process is None:
            if now >= self.retry_at:
                self._spawn()
            return
        code = self.process.poll()
        if code is not None:
            self._failed(f"exit: {code}")
            return
        heartbeat_at = self.heartbeat.time()
        if heartbeat_at is None:
            if now - self.started_at > self.startup_timeout:
                self._failed("startup timed out before first processed frame")
        elif now - heartbeat_at > self.stall_timeout:
            self._failed("processed frames stopped")
        else:
            self.retries.healthy(now)
            if self.state != "running":
                self.state = "running"
                self._log("frames_resumed")
                if self.generation > 1:
                    self.report("相机画面已恢复，站稳片刻即可接着玩。")

    def close(self):
        self.state = "closed"
        self._reap()
        for listener in self.listeners:
            listener.close()
        self.listeners.clear()

    def __exit__(self, *args):
        self.close()
