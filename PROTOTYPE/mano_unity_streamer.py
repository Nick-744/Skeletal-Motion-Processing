import cv2
import socket
import json

from MANOkinematics.config import (MANO_MODEL_PATH, MANO_MODEL_PATH_RIGHT)

from hand_engine import (HandTracker, model_path)
from hand_kinematics_3D import Hand3D
from mano_mesh_reconstruction import ManoHand



# --- UDP SETUP --- #
UDP_IP   = '127.0.0.1'
UDP_PORT = 5052
sock     = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)



def main(window_title: str = 'MANO to Unity Streamer') -> None:
    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    hand_calculator = Hand3D()

    hand_mano_left  = ManoHand(MANO_MODEL_PATH)
    hand_mano_right = ManoHand(MANO_MODEL_PATH_RIGHT)

    print(f'\n-> Streaming MANO poses to Unity on {UDP_IP}:{UDP_PORT}...\n')

    with HandTracker(model_path) as tracker: 
        while cap.isOpened():
            (success, frame) = cap.read()
            if not success: break;
            
            frame = cv2.flip(frame, 1)
            tracker.detect(frame)
            tracker.draw(frame)
            
            (hands_data, anchors_data) = hand_calculator.get_3d_coordinates(tracker.latest_result)
            
            # Dictionary to hold the data for this frame
            frame_payload = {
                'left_pose':  None,
                'right_pose': None
            }

            if hands_data and tracker.latest_result:
                for (i, hand_data) in enumerate(hands_data):
                    try:
                        handedness_label = tracker.latest_result.handedness[i][0].category_name
                    except IndexError:
                        handedness_label = 'Unknown'

                    if handedness_label == 'Left':
                        (_, pose_abs) = hand_mano_left.solve(hand_data)
                        if pose_abs is not None: frame_payload['left_pose'] = pose_abs.tolist()
                    
                    elif handedness_label == 'Right':
                        (_, pose_abs) = hand_mano_right.solve(hand_data)
                        if pose_abs is not None: frame_payload['right_pose'] = pose_abs.tolist()
            
            # Send data to Unity if at least one hand was detected
            if frame_payload['left_pose'] or frame_payload['right_pose']:
                message = json.dumps(frame_payload).encode('utf-8')
                sock.sendto(message, (UDP_IP, UDP_PORT))

            cv2.imshow(window_title, frame)
            
            if (cv2.waitKey(1) & 0xFF == 27) or \
                cv2.getWindowProperty(window_title, cv2.WND_PROP_VISIBLE) < 1:
                break;

    # Cleanup
    cap.release()
    cv2.destroyAllWindows()

    return;



if __name__ == '__main__':
    main()
