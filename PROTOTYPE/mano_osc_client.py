import cv2
import numpy as np
from pythonosc import udp_client

from MANOkinematics.config import (MANO_MODEL_PATH, MANO_MODEL_PATH_RIGHT)

from hand_engine import (HandTracker, model_path)
from hand_kinematics_3D import Hand3D
from mano_mesh_reconstruction import ManoHand



def main() -> None:
    # Set up OSC Client
    osc_ip     = '127.0.0.1'
    osc_port   = 8000
    osc_client = udp_client.SimpleUDPClient(osc_ip, osc_port)
    print(f'OSC Client initialized. Sending to {osc_ip}:{osc_port}')

    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    hand_calculator = Hand3D()

    hand_mano_left  = ManoHand(MANO_MODEL_PATH)
    hand_mano_right = ManoHand(MANO_MODEL_PATH_RIGHT)

    with HandTracker(model_path) as tracker:
        try:
            while cap.isOpened():
                (success, frame) = cap.read()
                if not success: break;
                
                frame = cv2.flip(frame, 1)
                tracker.detect(frame)
                
                (hands_data, anchors_data) = hand_calculator.get_3d_coordinates(tracker.latest_result)
                
                if hands_data and tracker.latest_result:
                    for (i, hand_data) in enumerate(hands_data):
                        try:
                            handedness_label = tracker.latest_result.handedness[i][0].category_name
                        except IndexError:
                            handedness_label = 'Unknown'

                        raw_x = anchors_data[i][0][0]
                        raw_y = anchors_data[i][1][0]
                        raw_z = anchors_data[i][2][0]

                        # Process the raw anchor data...
                        anchor_point = np.array([
                            ( raw_x - 0.5) * 0.6,
                            ( raw_y - 0.5) * 0.6,
                            (-raw_z + 0.5) * 0.12 + 0.3
                        ])

                        if handedness_label == 'Left':
                            (vertices, pose_abs) = hand_mano_left.solve(hand_data)
                            if vertices is not None:
                                # Send Left Hand OSC Data
                                osc_client.send_message('/mano/left/root', anchor_point.tolist())
                                osc_client.send_message('/mano/left/pose', pose_abs.flatten().tolist())
                        
                        elif handedness_label == 'Right':
                            (vertices, pose_abs) = hand_mano_right.solve(hand_data)
                            if vertices is not None:
                                # Send Right Hand OSC Data
                                osc_client.send_message('/mano/right/root', anchor_point.tolist())
                                osc_client.send_message('/mano/right/pose', pose_abs.flatten().tolist())
        
        except KeyboardInterrupt: print('\nStopping OSC stream...')

    # Cleanup
    cap.release()
    cv2.destroyAllWindows()

    return;



if __name__ == '__main__':
    main()
