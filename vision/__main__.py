import argparse
import json
import sys
import time
from pathlib import Path

from .bridge import PoseBridge
from .protocol import FrameFactory

ROOT = Path(__file__).resolve().parents[1]


def run_demo(args, bridge):
    from .demo import landmarks_at
    factory = FrameFactory()
    start = time.monotonic()
    while not args.seconds or time.monotonic()-start < args.seconds:
        bridge.publish(factory.make(landmarks_at(time.monotonic()-start)))
        time.sleep(1/30)


def run_camera(args, bridge):
    import cv2
    from .model import create_landmarker, model_image
    if not args.model.is_file():
        raise RuntimeError("模型未准备好，请先运行 scripts/setup.sh。")
    factory = FrameFactory()
    camera = cv2.VideoCapture(args.camera, cv2.CAP_AVFOUNDATION if sys.platform == "darwin" else cv2.CAP_ANY)
    try:
        if not camera.isOpened():
            raise RuntimeError("无法打开摄像头。请检查 macOS 相机权限，以及其他程序是否占用相机。")
        camera.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        camera.set(cv2.CAP_PROP_FPS, 30)
        previous_stamp = -1
        failures = 0
        frames = poses = 0
        inference_seconds = 0
        with create_landmarker(args.model) as model:
            start = time.monotonic()
            while not args.seconds or time.monotonic()-start < args.seconds:
                ok, image = camera.read()
                captured_ms = int(time.time()*1000)
                if not ok:
                    bridge.publish(factory.make(captured_ms=captured_ms))
                    failures += 1
                    if failures >= 10:
                        raise RuntimeError("摄像头连续没有返回画面，请检查相机连接和权限。")
                    time.sleep(.05)
                    continue
                failures = 0
                stamp = max(previous_stamp+1, int((time.monotonic()-start)*1000))
                previous_stamp = stamp
                inference_start = time.monotonic()
                result = model.detect_for_video(model_image(image), stamp)
                inference_seconds += time.monotonic()-inference_start
                frames += 1
                landmarks = result.pose_landmarks[0] if result.pose_landmarks else None
                poses += bool(landmarks)
                bridge.publish(factory.make(landmarks, captured_ms))
                if not args.no_preview:
                    h,w = image.shape[:2]
                    if landmarks:
                        for a,b in [(11,12),(11,13),(13,15),(12,14),(14,16),(11,23),(12,24),(23,24)]:
                            if min(landmarks[a].visibility,landmarks[b].visibility) >= .5:
                                pa,pb = landmarks[a],landmarks[b]
                                cv2.line(image,(int(pa.x*w),int(pa.y*h)),(int(pb.x*w),int(pb.y*h)),(220,220,20),3)
                    image = cv2.flip(image,1)
                    cv2.putText(image,"LOCAL CAMERA | Q: quit | no recording",(15,28),cv2.FONT_HERSHEY_SIMPLEX,.6,(255,255,255),2)
                    cv2.imshow("UltramanGame - Camera",image)
                    if cv2.waitKey(1)&255 in (ord('q'),27):
                        break
            elapsed = time.monotonic()-start
            print(json.dumps({"frames":frames,"pose_frames":poses,
                "elapsed_seconds":round(elapsed,2),"processed_fps":round(frames/max(elapsed,.001),1),
                "mean_inference_ms":round(inference_seconds*1000/max(frames,1),1)}),flush=True)
    finally:
        bridge.publish(factory.make())
        camera.release()
        if not args.no_preview:
            cv2.destroyAllWindows()


def main():
    parser = argparse.ArgumentParser(description="本地摄像头姿态服务；仅通过回环地址发送关键点，不录制视频。")
    parser.add_argument("--demo",action="store_true",help="合成姿态测试，不打开摄像头")
    parser.add_argument("--camera",type=int,default=0)
    parser.add_argument("--port",type=int,default=8765)
    parser.add_argument("--seconds",type=float,default=0,help="测试运行秒数，0 表示持续运行")
    parser.add_argument("--no-preview",action="store_true")
    parser.add_argument("--model",type=Path,default=ROOT/"models/pose_landmarker_lite.task")
    args = parser.parse_args()
    if not 1 <= args.port <= 65535 or args.seconds < 0:
        parser.error("port 必须在 1–65535；seconds 不能为负数")
    try:
        with PoseBridge(args.port) as bridge:
            print(f"{'合成姿态' if args.demo else '本地摄像头'}服务：127.0.0.1:{args.port}",flush=True)
            (run_demo if args.demo else run_camera)(args,bridge)
    except KeyboardInterrupt:
        return 0
    except (RuntimeError,OSError) as exc:
        print(f"启动失败：{exc}",file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
