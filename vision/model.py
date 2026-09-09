"""Shared native inference setup, also exercised by the camera-free smoke check."""
import cv2
import mediapipe as mp
from mediapipe.tasks.python import BaseOptions
from mediapipe.tasks.python.vision import PoseLandmarker, PoseLandmarkerOptions, RunningMode

# 0.10.21 + XNNPACK CPU passed the fresh-frame endurance check on Apple M3.
# The former 1.0.1 Metal path exhausted pixel buffers after about 16k frames.
# Kept as a diagnostic override for scripts/soak_model.py, not a game setting.
USE_METAL = False


def create_landmarker(path):
    delegate = BaseOptions.Delegate.GPU if USE_METAL else BaseOptions.Delegate.CPU
    return PoseLandmarker.create_from_options(PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=str(path), delegate=delegate),
        running_mode=RunningMode.VIDEO, num_poses=1,
        min_pose_detection_confidence=.5, min_pose_presence_confidence=.5,
        min_tracking_confidence=.5))


def model_image(bgr):
    # The Mac GPU path requires four channels; SRGB fails in its pixel-buffer conversion.
    converted = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGBA if USE_METAL else cv2.COLOR_BGR2RGB)
    return mp.Image(image_format=mp.ImageFormat.SRGBA if USE_METAL else mp.ImageFormat.SRGB,
                    data=converted)
