"""Shared native inference setup, also exercised by the camera-free smoke check."""
import sys

import cv2
import mediapipe as mp
from mediapipe.tasks.python import BaseOptions
from mediapipe.tasks.python.vision import PoseLandmarker, PoseLandmarkerOptions, RunningMode

USE_METAL = sys.platform == "darwin"


def create_landmarker(path):
    # MediaPipe 1.0.1's macOS CPU graph aborts while opening a Metal service.
    # Explicit GPU + SRGBA was verified on the target Apple M3.
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
