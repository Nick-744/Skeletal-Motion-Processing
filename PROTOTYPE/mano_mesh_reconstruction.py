import cv2
import numpy as np
import transforms3d
import open3d as o3d

from MANOkinematics.AIK import adaptive_IK
from MANOkinematics.models import KinematicModel
from MANOkinematics.armatures import MANOArmature
from MANOkinematics.config import (MANO_MODEL_PATH, MANO_MODEL_PATH_RIGHT)

from hand_engine import (HandTracker, reverse_handedness, model_path)
from hand_kinematics_3D import (Hand3D, HandDataRaw)

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



class ManoHand:
    def __init__(self, model_path: str):
        self.kinematic_model = KinematicModel(model_path, MANOArmature)
        
        # T-pose template (zeroed absolute pose)
        (self.initial_vertices, template_mano) = self.kinematic_model.set_params(pose_abs = np.zeros((16, 3)))

        # MediaPipe -> MANO
        mp_indices_for_mano_joints = [
            0,              # MP       0: Wrist  -> MANO 0 ('W')
            13, 14, 15, 20, # MP  1 -  4: Thumb  -> MANO T0, T1, T2, T3
             1,  2,  3, 16, # MP  5 -  8: Index  -> MANO I0, I1, I2, I3
             4,  5,  6, 17, # MP  9 - 12: Middle -> MANO M0, M1, M2, M3
            10, 11, 12, 19, # MP 13 - 16: Ring   -> MANO R0, R1, R2, R3
             7,  8,  9, 18  # MP 17 - 20: Pinky  -> MANO L0, L1, L2, L3
        ]
        self.template_kpts = template_mano[mp_indices_for_mano_joints]

        return;

    def solve(self, hands_data: HandDataRaw) -> tuple[np.ndarray, np.ndarray]:
        '''
        Returns (vertices, pose_abs)
        - vertices: The 3D mesh points.
        - pose_abs: The raw joint rotations (axis-angle).
        '''

        if not hands_data: return (None, None);
        
        (xw, yw, zw) = hands_data[0]
        joints       = np.vstack((xw, yw, zw)).T

        template = self.template_kpts
        ratio    = np.linalg.norm(template[9] - template[0]) / (np.linalg.norm(joints[9] - joints[0]) + 1e-8)
        
        j3d_pre_process = joints * ratio
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
                    pose_abs[i] = axis * angle
            except ValueError:
                pose_abs[i] = np.array([0.0, 0.0, 0.0])

        # Apply the solved Axis-Angle rotations directly to the model.
        # pose_abs already includes the root rotation at index 0.
        (vertices, _) = self.kinematic_model.set_params(pose_abs = pose_abs)

        return (vertices, pose_abs);



class ManoVisualizer:
    ''' Responsible ONLY for drawing the hand mesh using Open3D. '''

    def __init__(self, initial_vertices: np.ndarray, faces: np.ndarray):
        # Open3D Setup
        self.vis = o3d.visualization.Visualizer()
        self.vis.create_window(window_name = 'MANO 3D Hand', width = 800, height = 600)
        
        # Initialize an Open3D TriangleMesh
        self.mesh           = o3d.geometry.TriangleMesh()
        self.mesh.vertices  = o3d.utility.Vector3dVector(initial_vertices)
        self.mesh.triangles = o3d.utility.Vector3iVector(faces) # Faces remain constant!
        self.mesh.compute_vertex_normals()
        self.mesh.paint_uniform_color([0.7, 0.7, 0.7])

        # Add the mesh to the visualizer
        self.vis.add_geometry(self.mesh)

        # Set the "up" direction to be along the negative Y-axis...
        view_control = self.vis.get_view_control()
        view_control.set_up([0, -1, 0])
        view_control.set_front([0, 0, -1]) # Keep the camera looking straight ahead...
        
        # Open3D Render Options
        opt                  = self.vis.get_render_option()
        opt.background_color = np.asarray([0.1, 0.1, 0.1])

        return;

    def render(self, vertices: np.ndarray) -> None:
        ''' Just updates the mesh and draws it. '''

        self.mesh.vertices = o3d.utility.Vector3dVector(vertices)
        self.mesh.compute_vertex_normals()

        self.vis.update_geometry(self.mesh)
        self.vis.update_renderer() # Re-render Open3D

        return;



def main(window_title: str = 'Testing MANO') -> None:
    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    hand_calculator = Hand3D()
    hand_mano_left  = ManoHand(MANO_MODEL_PATH)
    mano_visualizer = ManoVisualizer(
        hand_mano_left.initial_vertices,
        hand_mano_left.kinematic_model.faces
    )

    with HandTracker(model_path) as tracker: 
        while cap.isOpened():
            if not mano_visualizer.vis.poll_events(): break; # Exit if the Open3D window is closed...
            
            (success, frame) = cap.read()
            if not success: break;
            
            frame = cv2.flip(frame, 1)
            tracker.detect(frame)
            tracker.draw(frame)
            
            (hands_data, anchors_data) = hand_calculator.get_3d_coordinates(tracker.latest_result)
            
            if hands_data and tracker.latest_result:
                # - Note: Because of the flipping (Selfie mode), the handedness is reversed...
                # As a result, the right hand uses the left MANO model and the left hand uses the right MANO model!
                handedness_label = reverse_handedness(tracker.latest_result.handedness[0][0].category_name)

                (vertices, _) = hand_mano_left.solve(hands_data)
                
                if vertices is not None: mano_visualizer.render(vertices)

            cv2.imshow(window_title, frame)
            
            # Check if the window is closed or if the ESC key is pressed to exit the loop...
            if (cv2.waitKey(1) & 0xFF == 27) or \
                cv2.getWindowProperty(window_title, cv2.WND_PROP_VISIBLE) < 1:
                break;

    # Cleanup
    cap.release()
    cv2.destroyAllWindows()
    mano_visualizer.vis.destroy_window()

    return;



if __name__ == '__main__':
    main()
