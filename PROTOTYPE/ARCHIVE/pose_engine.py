import os

os.environ['TF_ENABLE_ONEDNN_OPTS'] = '0'
'''
I tensorflow/core/util/port.cc:153] oneDNN custom operations are on.
You may see slightly different numerical results due to floating-point
round-off errors from different computation orders. To turn them off,
set the environment variable `TF_ENABLE_ONEDNN_OPTS=0`.
'''

import threading
from time import time
from typing import NewType
from collections import deque

import cv2
import mediapipe as mp



# ---< Paths >--- #
current_dir = os.path.dirname(os.path.realpath(__file__))
model_path  = os.path.join(current_dir, 'ASSETS', 'Google_AI_Edge', 'pose_landmarker_full.task')

# ---< Shortcuts >--- #
mp_pose_connections = mp.tasks.vision.PoseLandmarksConnections
mp_drawing          = mp.tasks.vision.drawing_utils
mp_drawing_styles   = mp.tasks.vision.drawing_styles

BaseOptions           = mp.tasks.BaseOptions
PoseLandmarker        = mp.tasks.vision.PoseLandmarker
PoseLandmarkerOptions = mp.tasks.vision.PoseLandmarkerOptions
PoseLandmarkerResult  = NewType('PoseLandmarkerResult', mp.tasks.vision.PoseLandmarkerResult)
VisionRunningMode     = mp.tasks.vision.RunningMode



class PoseTracker:
    def __init__(self, model_path: str, num_poses: int = 1):
        self._latest_result = None # PoseLandmarkerResult | None
        self._lock          = threading.Lock()

        # Create a pose landmarker instance with the live stream mode:
        options = PoseLandmarkerOptions(
            base_options    = BaseOptions(model_asset_path = model_path),
            num_poses       = num_poses,
            running_mode    = VisionRunningMode.LIVE_STREAM,
            result_callback = self._on_result
        )

        # The pose landmarker is initialized...
        self._pose_landmarker = PoseLandmarker.create_from_options(options)

        return;

    def _on_result(self, result: PoseLandmarkerResult, output_image: mp.Image, timestamp_ms: int) -> None:
        ''' Internal callback (PoseLandmarkerOptions.result_callback). '''
        with self._lock:
            self._latest_result = result
        
        return;

    @property # Access a method as if it were an attribute...
    def latest_result(self) -> PoseLandmarkerResult | None:
        ''' Get the latest pose landmark detection result.
            Returns "None" if no result is available yet. '''
        with self._lock:
            return self._latest_result;

    def detect(self, bgr_frame: 'cv2.Mat') -> None:
        ''' Send a BGR OpenCV frame for async detection...
            The results will be available via the "latest_result" property. '''
        
        # Convert the frame received from OpenCV to a MediaPipe’s Image object.
        rgb      = cv2.cvtColor(bgr_frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format = mp.ImageFormat.SRGB, data = rgb)

        # Send live image data to perform pose landmarking.
        # The results are accessible via the "result_callback" provided in
        # the "PoseLandmarkerOptions" object!
        # The pose landmarker must be created with the live stream mode...
        self._pose_landmarker.detect_async(mp_image, int(time() * 1e3))

        return;

    def draw(self, frame: 'cv2.Mat') -> None:
        ''' Overlay pose landmarks on frame in-place. '''

        result = self.latest_result
        if result is None: return;
        
        for pose_landmarks in result.pose_landmarks:
            mp_drawing.draw_landmarks(
                frame,
                pose_landmarks,
                mp_pose_connections.POSE_LANDMARKS,
                mp_drawing_styles.get_default_pose_landmarks_style()
            )
        
        return;

    # ---< Cleanup resources >--- #
    def close(self): self._pose_landmarker.close()
    
    def __enter__(self): return self; # Context-manager support

    def __exit__(self, *_): self.close()



def run(window_title: str = 'Testing Pose...') -> None:
    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    # Moving average FPS calculation
    frame_times = deque(maxlen = 30)
    prev_time   = time() # Initialize time for FPS calculation

    with PoseTracker(model_path) as tracker:
        while True:
            (success, frame) = cap.read()
            if not success: break;

            # Moving average FPS calculation
            current_time = time()
            frame_times.append(current_time - prev_time)
            prev_time = current_time
            avg_delta = sum(frame_times) / len(frame_times) # Average the last [x] samples
            fps       = 1 / avg_delta if avg_delta > 0 else 0

            frame = cv2.flip(frame, 1) # Flip the image horizontally / Selfie...
            tracker.detect(frame)
            tracker.draw(frame)

            # Render FPS on frame
            cv2.putText(
                frame, f'FPS: {int(fps)}',
                (15, 40),
                cv2.FONT_HERSHEY_SIMPLEX,
                1, (0, 255, 0), 2
            )

            cv2.imshow(window_title, frame)
    
            # Check if the window is closed or if the ESC key is pressed to exit the loop...
            if (cv2.waitKey(1) & 0xFF == 27) or \
                cv2.getWindowProperty(window_title, cv2.WND_PROP_VISIBLE) < 1:
                break;

    cap.release()
    cv2.destroyAllWindows()

    return;



if __name__ == '__main__':
    run()
