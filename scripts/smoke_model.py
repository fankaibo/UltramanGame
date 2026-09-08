"""Load the actual model and run generated black frames; does not open the camera."""
import json
import time
import sys
from pathlib import Path
import numpy as np

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root))
from vision.model import create_landmarker, model_image, mp, USE_METAL

with create_landmarker(root/'models/pose_landmarker_lite.task') as model:
    image=model_image(np.zeros((480,640,3),dtype=np.uint8))
    started=time.monotonic()
    poses=0
    for i in range(5):poses+=len(model.detect_for_video(image,i*33).pose_landmarks)
    print(json.dumps({'test':'generated-black-frames','frames':5,'poses':poses,
                      'elapsed_ms':round((time.monotonic()-started)*1000,1),'mediapipe':mp.__version__,
                      'backend':'Metal GPU' if USE_METAL else 'CPU'}))
