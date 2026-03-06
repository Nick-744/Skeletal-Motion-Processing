import cv2
import trimesh
import pyrender
import numpy as np
import transforms3d

from MANOkinematics.AIK import adaptive_IK
from MANOkinematics.smoother import OneEuroFilter
from MANOkinematics.models import KinematicModel
from MANOkinematics.armatures import MANOArmature
from MANOkinematics.config import (MANO_MODEL_PATH, MANO_MODEL_PATH_RIGHT)

from mediapipeHLD import (HandTracker, model_path)
from visualize3D import Hand3D

import os
os.environ['KMP_DUPLICATE_LIB_OK'] = 'TRUE'
'''
OMP: Error #15: Initializing libiomp5md.dll, but found libiomp5md.dll already initialized.
OMP: Hint This means that multiple copies of the OpenMP runtime have been linked into the program.
That is dangerous, since it can degrade performance or cause incorrect results.
The best thing to do is to ensure that only a single OpenMP runtime is linked into the process,
e.g. by avoiding static linking of the OpenMP runtime in any library.
As an unsafe, unsupported, undocumented workaround you can set the environment variable KMP_DUPLICATE_LIB_OK=TRUE
to allow the program to continue to execute, but that may cause crashes or silently produce incorrect results.
For more information, please see http://www.intel.com/software/products/support/.
'''



class ManoHandVisualizer:
    def __init__(self, model_path: str):
        self.kinematic_model = KinematicModel(model_path, MANOArmature)
        
        # T-pose template (zeroed absolute pose)
        (_, template_mano) = self.kinematic_model.set_params(pose_abs = np.zeros((16, 3)))

        # MANO -> MediaPipe
        mano_to_mp_idx = [
            0,              # MP       0: Wrist  -> MANO 0 ('W')
            13, 14, 15, 20, # MP  1 -  4: Thumb  -> MANO T0, T1, T2, T3
             1,  2,  3, 16, # MP  5 -  8: Index  -> MANO I0, I1, I2, I3
             4,  5,  6, 17, # MP  9 - 12: Middle -> MANO M0, M1, M2, M3
            10, 11, 12, 19, # MP 13 - 16: Ring   -> MANO R0, R1, R2, R3
             7,  8,  9, 18  # MP 17 - 20: Pinky  -> MANO L0, L1, L2, L3
        ]
        self.template_kpts = template_mano[mano_to_mp_idx]

        self.filters = {
            'Right': OneEuroFilter(mincutoff = 2.0, beta = 0.05),
            'Left':  OneEuroFilter(mincutoff = 2.0, beta = 0.05)
        }

        # Pyrender Setup
        self.scene = pyrender.Scene(bg_color = [0.1, 0.1, 0.1])
        self.scene.add(pyrender.DirectionalLight(color = [1.0, 1.0, 1.0], intensity = 3.0))
        self.viewer    = pyrender.Viewer(self.scene, run_in_thread = True, use_raymond_lighting = True)
        self.hand_node = None

        return;

    def update(self, hands_data: tuple, anchors_data: tuple | None = None, handedness: str = 'Right') -> None:
        if not hands_data: return;
        
        (xw, yw, zw) = hands_data[0]
        # (ax, ay, az) = anchors_data[0]
        
        xs = xw
        ys = yw
        zs = zw
        
        raw_joints = np.vstack((xs, ys, zs)).T
        
        is_left = (handedness == 'Left')
        if is_left:
            raw_joints[:, 0] *= -1.0

        filtered_joints = self.filters[handedness].process(raw_joints)

        template = self.template_kpts
        ratio    = np.linalg.norm(template[9] - template[0]) / \
            (np.linalg.norm(filtered_joints[9] - filtered_joints[0]) + 1e-8)
        
        j3d_pre_process = filtered_joints * ratio
        j3d_pre_process = j3d_pre_process - j3d_pre_process[0] + template[0]

        pose_R = adaptive_IK(T_ = template, P_ = j3d_pre_process) 
        
        pose_abs = np.zeros((16, 3))
        for i in range(16):
            rotation_matrix = pose_R[0, i] 
            if np.allclose(rotation_matrix, 0.0):
                rotation_matrix = np.eye(3)
            try:
                (axis, angle) = transforms3d.axangles.mat2axangle(rotation_matrix)
                if np.isnan(axis).any():
                    pose_abs[i] = np.array([0.0, 0.0, 0.0])
                else:
                    # --- FIX INVERTED FINGERS --- #
                    if i == 0:
                        pose_abs[i] = axis * angle
                    else:
                        # For the fingers (i > 0): if the space was mirrored,
                        # the IK cross-products calculate inverted bends...
                        if is_left: 
                            pose_abs[i] = -(axis * angle)
                        else:
                            pose_abs[i] = axis * angle
            except ValueError:
                pose_abs[i] = np.array([0.0, 0.0, 0.0])

        # Apply the solved Axis-Angle rotations directly to the model.
        # pose_abs already includes the root rotation at index 0.
        (vertices, _) = self.kinematic_model.set_params(pose_abs = pose_abs)
        
        # Mirror vertices back for rendering if dealing with a Left Hand
        if is_left: vertices[:, 0] *= -1.0

        self.viewer.render_lock.acquire()
        try:
            if self.hand_node: self.scene.remove_node(self.hand_node)
            mesh           = pyrender.Mesh.from_trimesh(
                trimesh.Trimesh(vertices, self.kinematic_model.faces)
            )
            self.hand_node = self.scene.add(mesh)
        finally:
            self.viewer.render_lock.release()

        return;



def main(window_title: str = 'Testing MANO') -> None:
    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    hand_calculator = Hand3D()
    visualizer      = ManoHandVisualizer(MANO_MODEL_PATH)

    with HandTracker(model_path) as tracker: 
        while cap.isOpened() and visualizer.viewer.is_active:
            (success, frame) = cap.read()
            if not success: break;
            
            frame = cv2.flip(frame, 1)
            tracker.detect(frame)
            tracker.draw(frame)
            
            result          = hand_calculator.get_3d_coordinates(tracker.latest_result)
            (hands_data, _) = result
            
            if hands_data and tracker.latest_result and tracker.latest_result.handedness:
                handedness_label = tracker.latest_result.handedness[0][0].category_name
                visualizer.update(hands_data, handedness = handedness_label)
            
            cv2.imshow(window_title, frame)
            
            # Check if the window is closed or if the ESC key is pressed to exit the loop...
            if (cv2.waitKey(1) & 0xFF == 27) or \
                cv2.getWindowProperty(window_title, cv2.WND_PROP_VISIBLE) < 1:
                break;

    # Cleanup
    cap.release()
    cv2.destroyAllWindows()
    if visualizer.viewer.is_active:
        visualizer.viewer.close_external()

    return;



if __name__ == '__main__':
    main()
