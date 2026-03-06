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
model_path  = os.path.join(current_dir, 'ASSETS', 'Google_AI_Edge', 'gesture_recognizer.task')

# ---< Shortcuts >--- #
mp_hands          = mp.tasks.vision.HandLandmarksConnections
mp_drawing        = mp.tasks.vision.drawing_utils
mp_drawing_styles = mp.tasks.vision.drawing_styles

BaseOptions              = mp.tasks.BaseOptions
GestureRecognizer        = mp.tasks.vision.GestureRecognizer
GestureRecognizerOptions = mp.tasks.vision.GestureRecognizerOptions
GestureRecognizerResult  = NewType('GestureRecognizerResult', mp.tasks.vision.GestureRecognizerResult) # Better type hints!
VisionRunningMode        = mp.tasks.vision.RunningMode



class HandTracker:
    def __init__(self, model_path: str, num_hands: int = 2):
        self._latest_result = None # GestureRecognizerResult | None
        self._lock          = threading.Lock()

        # Create a gesture recognizer instance with the live stream mode:
        options = GestureRecognizerOptions(
            base_options    = BaseOptions(model_asset_path = model_path),
            num_hands       = num_hands,
            running_mode    = VisionRunningMode.LIVE_STREAM,
            result_callback = self._on_result
        )

        # The gesture recognizer (+ integrated hand landmarker) is initialized...
        self._gesture_recognizer = GestureRecognizer.create_from_options(options)

        return;

    def _on_result(self, result: GestureRecognizerResult, output_image: mp.Image, timestamp_ms: int) -> None:
        ''' Internal callback (HandLandmarkerOptions.result_callback). '''
        with self._lock:
            self._latest_result = result

        return;

    @property # Access a method as if it were an attribute...
    def latest_result(self) -> GestureRecognizerResult | None:
        ''' Get the latest hand landmark detection result.
            Returns "None" if no result is available yet. '''
        with self._lock:
            return self._latest_result;

    def detect(self, bgr_frame: 'cv2.Mat') -> None:
        ''' Send a BGR OpenCV frame for async detection...
            The results will be available via the "latest_result" property. '''

        # Convert the frame received from OpenCV to a MediaPipe’s Image object.
        rgb      = cv2.cvtColor(bgr_frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format = mp.ImageFormat.SRGB, data = rgb)

        # Send live image data to perform gesture recognition (+ hand landmarks detection).
        # The results are accessible via the "result_callback" provided in
        # the "GestureRecognizerOptions" object!
        # The gesture recognizer must be created with the live stream mode...
        self._gesture_recognizer.recognize_async(mp_image, int(time() * 1e6))

        return;

    def draw(self, frame: 'cv2.Mat') -> None:
        ''' Overlay hand landmarks on frame in-place. '''

        result = self.latest_result
        if result is None: return;
    
        for hand_landmarks in result.hand_landmarks:
            mp_drawing.draw_landmarks(
                frame,
                hand_landmarks,
                mp_hands.HAND_CONNECTIONS,
                mp_drawing_styles.get_default_hand_landmarks_style(),
                mp_drawing_styles.get_default_hand_connections_style(),
            )

        return;

    # ---< Cleanup resources >--- #
    def close(self): self._gesture_recognizer.close()
    
    def __enter__(self): return self; # Context-manager support

    def __exit__(self, *_): self.close()



# ---< Helper functions >--- #
def reverse_handedness(handedness: str) -> str:
    ''' Reverse the handedness label.
        Use if input image is flipped horizontally (Selfie). '''

    if handedness == 'Left':  return 'R';
    if handedness == 'Right': return 'L';

    return '?';

def render_gesture_result(frame: 'cv2.Mat', gesture_result: GestureRecognizerResult) -> None:
    ''' Render gesture recognition results on frame in-place. '''

    if gesture_result.gestures:
        gesture_name = gesture_result.gestures[0][0].category_name
        handedness   = gesture_result.handedness[0][0].category_name
        cv2.putText(
            frame, f'{reverse_handedness(handedness)}: {gesture_name}',
            (15, 80),
            cv2.FONT_HERSHEY_SIMPLEX,
            1, (255, 0, 0), 2
        )

        if len(gesture_result.gestures) > 1:
            gesture_name = gesture_result.gestures[1][0].category_name
            handedness   = gesture_result.handedness[1][0].category_name
            cv2.putText(
                frame, f'{reverse_handedness(handedness)}: {gesture_name}',
                (15, 120),
                cv2.FONT_HERSHEY_SIMPLEX,
                1, (255, 0, 0), 2
            )

    return;



def run(window_title: str = 'Testing...') -> None:
    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    # Moving average FPS calculation
    frame_times = deque(maxlen = 30)
    prev_time   = time() # Initialize time for FPS calculation

    with HandTracker(model_path) as tracker:
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

            # Render gesture recognition results on frame
            if tracker.latest_result is not None:
                render_gesture_result(frame, tracker.latest_result)

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
