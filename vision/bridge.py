"""One camera owner, multiple loopback subscribers; only the latest pose is kept."""
import socket
import socketserver
import threading

from .protocol import encode


class _Handler(socketserver.BaseRequestHandler):
    def handle(self):
        bridge = self.server.bridge
        seen = 0
        self.request.settimeout(1.0)
        self.request.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        while not bridge.stopping.is_set():
            with bridge.changed:
                bridge.changed.wait_for(lambda: bridge.revision != seen or bridge.stopping.is_set(), timeout=1)
                if bridge.stopping.is_set():
                    break
                if seen == bridge.revision:
                    continue
                seen, line = bridge.revision, bridge.latest
            try:
                self.request.sendall(line)
            except (OSError, TimeoutError):
                break


class _Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


class LatestBridge:
    def __init__(self, port=8765, handler=_Handler):
        self.changed = threading.Condition()
        self.stopping = threading.Event()
        self.revision = 0
        self.latest = b""
        self.server = _Server(("127.0.0.1", port), handler)
        self.server.bridge = self
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    @property
    def address(self):
        return self.server.server_address

    def publish(self, line):
        with self.changed:
            self.latest = line
            self.revision += 1
            self.changed.notify_all()

    def __enter__(self):
        self.thread.start()
        return self

    def __exit__(self, *args):
        self.stopping.set()
        with self.changed:
            self.changed.notify_all()
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)


class PoseBridge(LatestBridge):
    def publish(self, frame):
        super().publish(encode(frame))
