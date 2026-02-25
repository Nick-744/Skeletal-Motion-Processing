import cv2
import torch
import smplx
import trimesh
import pyrender
from scipy.spatial.transform import Rotation as R
from mediapipeHLD import (HandTracker, model_path)

# **Absolute path** to the MANO model directory...
MANO_MODEL_PATH = r'C:\Users\nick1\Documents\GitHub\my_thesis\PROTOTYPE\ASSETS'

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



# ---< Compatibility bs fixing >--- #
import numpy as np
import inspect
import sys

np.bool    = bool
np.int     = int
np.float   = float
np.complex = complex
np.object  = object
np.unicode = str
np.str     = str

if not hasattr(inspect, 'getargspec'):
    inspect.getargspec = inspect.getfullargspec

sys.modules['numpy.int'  ] = int
sys.modules['numpy.float'] = float
sys.modules['numpy.bool' ] = bool
#####################################



class ManoHandVisualizer:
    def __init__(self, model_path: str):
        self.mano_model = smplx.create(
            model_path     = model_path, 
            model_type     = 'mano', 
            is_rhand       = True, 
            use_pca        = False, 
            flat_hand_mean = True
        )
        
        self.scene  = pyrender.Scene(bg_color = [0.1, 0.1, 0.1])
        light = pyrender.DirectionalLight(color = [1.0, 1.0, 1.0], intensity = 3.0)
        self.scene.add(light, pose = np.eye(4))
        
        self.viewer    = pyrender.Viewer(
            self.scene,
            run_in_thread        = True,
            use_raymond_lighting = True
        )
        self.hand_node = None

        return;

    def _compute_global_frame(self,
        p_start:   np.ndarray,
        p_end:     np.ndarray,
        up_vector: np.ndarray) -> np.ndarray:
        '''
        Creates a consistent orthonormal basis (rotation matrix) for a bone.
        Z-axis: Aligned with the bone (Start -> End)
        X-axis: Perpendicular to Bone and Up Vector (Side)
        Y-axis: Perpendicular to Z and X (Up correction)
        '''

        # 1. Forward Vector (Z-axis, along the bone)
        v_z    = np.array([p_end.x - p_start.x, p_end.y - p_start.y, p_end.z - p_start.z])
        norm_z = np.linalg.norm(v_z)
        if norm_z < 1e-8: return np.eye(3); # Handle zero length error
        v_z /= norm_z
        
        # 2. Right Vector (X-axis)
        v_x    = np.cross(up_vector, v_z)
        norm_x = np.linalg.norm(v_x)
        if norm_x < 1e-8: 
            # Fallback if bone and up vector are parallel
            v_x = np.array([1.0, 0.0, 0.0]) 
        else:
            v_x /= norm_x
            
        # 3. Up Vector (Y-axis, orthogonalized)
        v_y  = np.cross(v_z, v_x)
        v_y /= (np.linalg.norm(v_y) + 1e-8)
        
        # Construct Matrix (Column-major: [X, Y, Z])
        # This rotation transforms [0,0,1] to point along the bone
        rot_matrix = np.stack([v_x, v_y, v_z], axis=-1)

        return rot_matrix;

    def update(self, result: HandTracker.latest_result) -> None:
        if (not result) or (not result.hand_landmarks): return;

        lm = result.hand_landmarks[0]
        
        # MANO pose: 45 values (15 joints * 3 axis-angle values)
        pose_data = np.zeros(45)
        
        # --- STEP 1: Calculate Palm/Wrist Frame --- #
        # Calculate Palm Normal using Wrist(0), Index(5), Pinky(17)
        p_wrist = np.array([lm[0].x, lm[0].y, lm[0].z])
        p_index = np.array([lm[5].x, lm[5].y, lm[5].z])
        p_pinky = np.array([lm[17].x, lm[17].y, lm[17].z])
        
        v1 = p_index - p_wrist
        v2 = p_pinky - p_wrist
        palm_normal  = np.cross(v1, v2)
        palm_normal /= (np.linalg.norm(palm_normal) + 1e-8)

        # We define a "Global Wrist Rotation". 
        # Usually, we align the wrist to the Middle Finger MCP (9) for direction
        wrist_global_mat = self._compute_global_frame(lm[0], lm[9], palm_normal)

        # Store global rotations to compute relative child rotations
        # Key: MANO index, Value: Rotation Matrix
        global_rotations = { -1: wrist_global_mat } 

        # --- STEP 2: Map MediaPipe Chains to MANO ---
        # Structure: [Mano_Indices], [MP_Landmarks]
        # Chains allow us to track the parent joint easily
        chains = [
            # Index (Mano 0-2) -> MP 5-8
            {'mano': [ 0,  1,  2], 'mp': [ 5,  6,  7,  8], 'parent_idx': -1},
            # Middle (Mano 3-5) -> MP 9-12
            {'mano': [ 3,  4,  5], 'mp': [ 9, 10, 11, 12], 'parent_idx': -1},
            # Pinky (Mano 6-8) -> MP 17-20
            {'mano': [ 6,  7,  8], 'mp': [17, 18, 19, 20], 'parent_idx': -1},
            # Ring (Mano 9-11) -> MP 13-16
            {'mano': [ 9, 10, 11], 'mp': [13, 14, 15, 16], 'parent_idx': -1},
            # Thumb (Mano 12-14) -> MP 1-4 
            # Note: Thumb parent is also wrist (-1) in this simplified logic
            {'mano': [12, 13, 14], 'mp': [ 1,  2,  3,  4], 'parent_idx': -1},
        ]

        for chain in chains:
            previous_mano_idx = chain['parent_idx']
            
            # Iterate through the 3 joints of the finger (MCP, PIP, DIP)
            for i in range(3):
                mano_idx = chain['mano'][i]
                mp_start = chain['mp'][i]
                mp_end   = chain['mp'][i+1]
                
                # 1. Calculate Global Orientation of this segment
                current_global_mat         = self._compute_global_frame(lm[mp_start], lm[mp_end], palm_normal)
                global_rotations[mano_idx] = current_global_mat
                
                # 2. Get Parent's Global Rotation
                parent_global_mat = global_rotations[previous_mano_idx]
                
                # 3. Calculate Relative Rotation: R_local = inv(R_parent) * R_child
                # This removes the parent's rotation, leaving only the bending of this joint
                rel_mat = np.linalg.inv(parent_global_mat) @ current_global_mat
                
                # 4. Convert to Axis-Angle (Rotation Vector)
                rot_vec = R.from_matrix(rel_mat).as_rotvec()
                
                # Store
                start_idx = mano_idx * 3
                pose_data[start_idx : start_idx + 3] = rot_vec
                
                # Update parent for next iteration in this chain
                previous_mano_idx = mano_idx

        # --- STEP 3: Generate and Update Mesh ---
        # Note: We must clone pose_data to avoid memory layout issues with Torch
        target_pose = torch.tensor(pose_data, dtype=torch.float32).unsqueeze(0)

        output = self.mano_model(
            hand_pose    = target_pose,
            betas        = torch.zeros([1, 10]),
            return_verts = True
        )
        
        vertices = output.vertices.detach().cpu().numpy().squeeze()
        
        # Normalize position for visualization
        vertices -= np.mean(vertices, axis=0)
        vertices *= 15.0 

        self.viewer.render_lock.acquire()
        try:
            if self.hand_node:
                self.scene.remove_node(self.hand_node)
            new_mesh = pyrender.Mesh.from_trimesh(trimesh.Trimesh(vertices, self.mano_model.faces))
            self.hand_node = self.scene.add(new_mesh)
        finally:
            self.viewer.render_lock.release()
        
        return;

def main():
    cap        = cv2.VideoCapture(0)
    visualizer = ManoHandVisualizer(MANO_MODEL_PATH)

    with HandTracker(model_path) as tracker:
        while cap.isOpened():
            (success, frame) = cap.read()
            if not success: break;

            frame = cv2.flip(frame, 1)
            tracker.detect(frame)
            visualizer.update(tracker.latest_result)

    cap.release()
    cv2.destroyAllWindows()

    return;

if __name__ == "__main__":
    main()
