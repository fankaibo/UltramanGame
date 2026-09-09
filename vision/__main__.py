import argparse
import json
import socket
import sys
import time
from contextlib import ExitStack, nullcontext
from pathlib import Path

from .bridge import PoseBridge
from .protocol import FrameFactory

ROOT = Path(__file__).resolve().parents[1]


class FrameHeartbeat:
    """Advance only after frame processing, so a blocked native call is detectable."""
    def __init__(self, enabled):
        self.enabled = enabled
        self.next_at = 0

    def processed(self, sequence):
        now = time.monotonic()
        if self.enabled and now >= self.next_at:
            print(json.dumps({"event": "frame", "sequence": sequence}), flush=True)
            self.next_at = now + 1


def run_demo(args, bridge, preview=None, photo=None):
    from .demo import landmarks_at
    factory = FrameFactory(source="synthetic")
    start = time.monotonic()
    heartbeat = FrameHeartbeat(args.ready_json)
    while not args.seconds or time.monotonic()-start < args.seconds:
        points = landmarks_at(time.monotonic()-start)
        frame = factory.make(points)
        bridge.publish(frame)
        if preview:
            preview.publish(None, points, frame['capturedMs'], "synthetic")
        if photo:
            photo.publish(None, None, frame['capturedMs'], synthetic=True)
        heartbeat.processed(frame['sequence'])
        time.sleep(1/30)


def run_camera(args, bridge, preview=None, photo=None):
    import cv2
    from .capture import LatestCapture
    from .model import create_landmarker, model_image
    from .segmentation import PersonSegmenter
    if not args.model.is_file():
        raise RuntimeError("模型未准备好，请先运行 scripts/setup.sh。")
    factory = FrameFactory()
    def open_camera():
        camera = cv2.VideoCapture(args.camera, cv2.CAP_AVFOUNDATION if sys.platform == "darwin" else cv2.CAP_ANY)
        camera.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        camera.set(cv2.CAP_PROP_FPS, 30)
        camera.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        return camera
    previous_stamp = -1
    last_image_at = None
    frames = poses = revision = skipped = 0
    inference_seconds = 0
    photo_model = None
    photo_retry_at = 0
    heartbeat = FrameHeartbeat(args.ready_json)
    try:
        with create_landmarker(args.model) as model, LatestCapture(open_camera) as capture:
            start = time.monotonic()
            while not args.seconds or time.monotonic()-start < args.seconds:
                sample = capture.take(after=revision)
                if sample is None:
                    bridge.publish(factory.make())
                    waiting = time.monotonic()-(start if last_image_at is None else last_image_at)
                    if waiting >= (5 if last_image_at is None else 2):
                        raise RuntimeError("摄像头未返回画面，请检查相机连接、权限及其他程序占用。")
                    continue
                new_revision, image, captured_ms, captured_at = sample
                skipped += max(0, new_revision-revision-1)
                revision = new_revision
                last_image_at = time.monotonic()
                stamp = max(previous_stamp+1, int((captured_at-start)*1000))
                previous_stamp = stamp
                inference_start = time.monotonic()
                result = model.detect_for_video(model_image(image), stamp)
                inference_seconds += time.monotonic()-inference_start
                frames += 1
                landmarks = result.pose_landmarks[0] if result.pose_landmarks else None
                poses += bool(landmarks)
                bridge.publish(factory.make(landmarks, captured_ms))
                if preview:
                    preview.publish(image, landmarks, captured_ms)
                if photo and photo.due():
                    mask = None
                    try:
                        if time.monotonic() >= photo_retry_at:
                            if photo_model is None:
                                photo_model = PersonSegmenter()
                            mask = photo_model.mask(image)
                    except (RuntimeError, ValueError, OSError) as exc:
                        # A photo-only failure must not stop ordinary pose tracking.
                        print("合照人像暂不可用：" + type(exc).__name__, file=sys.stderr, flush=True)
                        photo_retry_at = time.monotonic() + 3
                        if photo_model is not None:
                            photo_model.close()
                            photo_model = None
                    photo.publish(image, mask, captured_ms)
                elif photo_model is not None and not photo.bridge.subscribers:
                    photo_model.close()
                    photo_model = None
                heartbeat.processed(frames)
                if not args.no_preview:
                    debug_image = image.copy()
                    if landmarks:
                        h,w = debug_image.shape[:2]
                        for a,b in ((11,12),(11,13),(13,15),(12,14),(14,16)):
                            if min(landmarks[a].visibility,landmarks[b].visibility)>=.55:
                                pa,pb=landmarks[a],landmarks[b]
                                cv2.line(debug_image,(int(pa.x*w),int(pa.y*h)),(int(pb.x*w),int(pb.y*h)),(220,220,20),3)
                    debug_image = cv2.flip(debug_image, 1)
                    cv2.putText(debug_image, "LOCAL CAMERA | Q: quit | no recording", (15,28), cv2.FONT_HERSHEY_SIMPLEX,.6,(255,255,255),2)
                    cv2.imshow("UltramanGame - Camera", debug_image)
                    if cv2.waitKey(1)&255 in (ord('q'),27):
                        break
            elapsed = time.monotonic()-start
            if frames == 0:
                raise RuntimeError("测试期间没有取得相机画面，请稍后重试。")
            print(json.dumps({"frames":frames,"pose_frames":poses,
                "elapsed_seconds":round(elapsed,2),"processed_fps":round(frames/max(elapsed,.001),1),
                "mean_inference_ms":round(inference_seconds*1000/max(frames,1),1),
                "skipped_capture_frames":skipped,"preview_frames":preview.frames if preview else 0}),flush=True)
    finally:
        if photo_model is not None:
            photo_model.close()
        bridge.publish(factory.make())
        if not args.no_preview:
            cv2.destroyAllWindows()


def main():
    parser = argparse.ArgumentParser(description="本地摄像头姿态与可选游戏预览服务；仅通过回环地址通信，不录制视频。")
    parser.add_argument("--demo",action="store_true",help="合成姿态测试，不打开摄像头")
    parser.add_argument("--camera",type=int,default=0)
    parser.add_argument("--port",type=int,default=8765,help="本机端口；0 由系统分配，供独立测试使用")
    parser.add_argument("--seconds",type=float,default=0,help="测试运行秒数，0 表示持续运行")
    parser.add_argument("--no-preview",action="store_true",help="关闭独立调试窗口，不影响游戏内预览")
    parser.add_argument("--game-preview",action="store_true",help="向游戏提供镜像画面及关节点（最多 320×240、约 15 FPS）")
    parser.add_argument("--preview-port",type=int,default=8766,help="游戏内预览的本机端口；0 由系统分配")
    parser.add_argument("--photo-port",type=int,default=8767,help="主动合照的本机端口；随 --game-preview 启用，0 由系统分配")
    parser.add_argument("--ready-json",action="store_true",help=argparse.SUPPRESS)
    for name in ("pose", "preview", "photo"):
        parser.add_argument("--" + name + "-fd", type=int, help=argparse.SUPPRESS)
    parser.add_argument("--model",type=Path,default=ROOT/"models/pose_landmarker_lite.task")
    args = parser.parse_args()
    if not all(0 <= p <= 65535 for p in (args.port, args.preview_port, args.photo_port)) or args.seconds < 0:
        parser.error("port 和 preview-port 必须在 0–65535；seconds 不能为负数")
    descriptors = (args.pose_fd, args.preview_fd, args.photo_fd)
    if any(fd is not None for fd in descriptors) and (not args.game_preview or
            any(fd is None or fd < 3 for fd in descriptors) or len(set(descriptors)) != 3):
        parser.error("继承的三个本机监听 socket 必须完整、独立，并启用 game-preview")
    try:
        with ExitStack() as sockets:
            listeners = [sockets.enter_context(socket.socket(fileno=fd)) if fd is not None else None
                         for fd in descriptors]
            bridge = sockets.enter_context(PoseBridge(args.port, listener=listeners[0]))
            log_stream = sys.stderr if args.ready_json else sys.stdout
            print(f"{'合成姿态' if args.demo else '本地摄像头'}服务：127.0.0.1:{bridge.address[1]}",file=log_stream,flush=True)
            from .preview import GamePreview
            from .photo import GamePhoto
            with (GamePreview(args.preview_port, listener=listeners[1]) if args.game_preview else nullcontext()) as preview, (GamePhoto(args.photo_port, listener=listeners[2]) if args.game_preview else nullcontext()) as photo:
                if preview:
                    print(f"游戏预览：127.0.0.1:{preview.bridge.address[1]}（不录制）",file=log_stream,flush=True)
                if args.ready_json:
                    print(json.dumps({"pose_port":bridge.address[1],"preview_port":preview.bridge.address[1] if preview else None,"photo_port":photo.bridge.address[1] if photo else None}),flush=True)
                (run_demo if args.demo else run_camera)(args,bridge,preview,photo)
    except KeyboardInterrupt:
        return 0
    except (RuntimeError,OSError) as exc:
        print(f"启动失败：{exc}",file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
