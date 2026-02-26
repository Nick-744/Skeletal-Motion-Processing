import cv2
import torch
import smplx
import trimesh
import pyrender
from scipy.spatial.transform import Rotation as R
from mediapipeHLD import (HandTracker, model_path)
from visualize3D import (HandData, Hand3D)

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
        # We use PCA (15 components) to constrain the hand to natural shapes,
        # which helps smooth out the jitter from MediaPipe.
        self.num_pca_comps = 15
        
        self.mano_model = smplx.create(
            model_path     = model_path, 
            model_type     = 'mano', 
            is_rhand       = True, 
            use_pca        = True, 
            num_pca_comps  = self.num_pca_comps, 
            flat_hand_mean = True
        )
        
        self.scene  = pyrender.Scene(bg_color = [0.1, 0.1, 0.1])
        light       = pyrender.DirectionalLight(color = [1.0, 1.0, 1.0], intensity = 3.0)
        self.scene.add(light, pose = np.eye(4))
        
        self.viewer = pyrender.Viewer(
            self.scene,
            run_in_thread        = True,
            use_raymond_lighting = True
        )
        self.hand_node = None

    def _compute_global_frame(self, 
        p_start:   np.ndarray, 
        p_end:     np.ndarray, 
        up_vector: np.ndarray) -> np.ndarray:
        '''
        Constructs a rotation-invariant basis for a bone segment.
        
        CRITICAL FIX for "Rotation Dependency":
        We ensure the basis is strictly defined by the bone vector (Y) 
        and the palm normal (used to derive Z/X), making it robust 
        to global hand rotation.
        
        Mapping to MANO (Right Hand):
        - Y-axis: Bone Vector (Start -> End)
        - X-axis: Flexion Axis (Points RIGHT relative to the bone)
        - Z-axis: Abduction Axis (Points UP/Out of palm)
        '''

        # 1. Y-Axis: The Bone Vector (Primary Axis)
        v_y = p_end - p_start
        norm_y = np.linalg.norm(v_y)
        if norm_y < 1e-8: return np.eye(3)
        v_y /= norm_y

        # 2. X-Axis: The Flexion Axis
        # We use Cross(Up, Y). 
        # Since 'Up' is the Palm Normal pointing OUT of the hand,
        # and Y is pointing towards the fingertips, 
        # Up x Y points to the RIGHT (Thumb side on right hand).
        # This aligns with MANO's expectation that +X rotation = Flexion.
        v_x = np.cross(up_vector, v_y)
        norm_x = np.linalg.norm(v_x)
        
        # Robustness check: If bone is perfectly parallel to palm normal (impossible anatomy but possible noise)
        if norm_x < 1e-8:
             # Fallback: Pick arbitrary perp vector, re-project later
            v_x = np.array([1.0, 0.0, 0.0]) if abs(v_y[0]) < 0.9 else np.array([0.0, 1.0, 0.0])
        
        v_x /= np.linalg.norm(v_x)

        # 3. Z-Axis: The Abduction Axis (Corrected Up)
        # We re-calculate Z as X cross Y to ensure a perfect orthonormal basis.
        # This makes Z point "Out" of the palm, orthogonal to the bone.
        v_z = np.cross(v_x, v_y)
        v_z /= np.linalg.norm(v_z)

        # Construct Rotation Matrix [Col_X, Col_Y, Col_Z]
        rot_matrix = np.stack([v_x, v_y, v_z], axis=-1)
        
        return rot_matrix

    def update(self, hands_data: HandData) -> None:
        if not hands_data: return

        (xs, ys, zs) = hands_data[0]
        def get_pt(idx): return np.array([xs[idx], ys[idx], zs[idx]])
        
        # 45 values: 15 joints * 3 axis-angle values
        full_pose_flat = np.zeros(45)

        # --- STEP 1: Define Global Palm Orientation ---
        p_wrist = get_pt(0)
        p_index = get_pt(5)
        p_pinky = get_pt(17)
        
        # Calculate Palm Normal (The "Up" Vector)
        # Vector from Wrist->Index and Wrist->Pinky defines the palm plane.
        # Cross product gives the normal pointing OUT of the palm.
        v_w_i = p_index - p_wrist
        v_w_p = p_pinky - p_wrist
        palm_normal = np.cross(v_w_p, v_w_i)
        palm_normal /= (np.linalg.norm(palm_normal) + 1e-8)

        # Compute Wrist Global Rotation
        # Note: We align the wrist frame to the Middle Finger Metacarpal (Wrist->MiddleMCP)
        # This is often more stable for MANO than IndexMCP.
        p_middle = get_pt(9)
        wrist_global_mat = self._compute_global_frame(p_wrist, p_middle, palm_normal)
        
        # Global Orient for MANO (Axis-Angle)
        wrist_rot_vec = R.from_matrix(wrist_global_mat).as_rotvec()

        # Storage for parent frames
        global_rotations = { -1: wrist_global_mat }

        # --- STEP 2: Calculate Finger Joint Rotations ---
        chains = [
            # Index (Mano 0-2)
            {'mano': [0, 1, 2], 'mp': [5, 6, 7, 8], 'parent_idx': -1},
            # Middle (Mano 3-5)
            {'mano': [3, 4, 5], 'mp': [9, 10, 11, 12], 'parent_idx': -1},
            # Ring (Mano 6-8) - Fixed indices
            {'mano': [6, 7, 8], 'mp': [13, 14, 15, 16], 'parent_idx': -1},
            # Pinky (Mano 9-11) - Fixed indices
            {'mano': [9, 10, 11], 'mp': [17, 18, 19, 20], 'parent_idx': -1},
            # Thumb (Mano 12-14)
            {'mano': [12, 13, 14], 'mp': [1, 2, 3, 4], 'parent_idx': -1},
        ]

        for chain in chains:
            previous_mano_idx = chain['parent_idx']
            
            for i in range(3):
                mano_idx = chain['mano'][i]
                mp_start = chain['mp'][i]
                mp_end   = chain['mp'][i+1]
                
                # 1. Compute Global Frame for this segment
                # Critical: We continue to use 'palm_normal' as the up-vector reference.
                # This enforces that all finger frames are consistent with the palm's orientation,
                # preventing the "twisting" artifacts when the hand rotates.
                child_global_mat = self._compute_global_frame(get_pt(mp_start), get_pt(mp_end), palm_normal)
                global_rotations[mano_idx] = child_global_mat
                
                # 2. Get Parent Global Frame
                parent_global_mat = global_rotations[previous_mano_idx]
                
                # 3. Compute Relative Rotation (Child relative to Parent)
                # R_rel = R_parent^T * R_child
                rel_mat = np.linalg.inv(parent_global_mat) @ child_global_mat
                
                # Convert to Axis-Angle
                # Optimization: Enforce continuity to avoid 360-degree flips
                rot_vec = R.from_matrix(rel_mat).as_rotvec()
                
                start_idx = mano_idx * 3
                full_pose_flat[start_idx : start_idx+3] = rot_vec
                
                previous_mano_idx = mano_idx

        # --- STEP 3: PCA Projection & Rendering ---
        
        # Get Mean and Components from MANO
        hand_mean       = self.mano_model.hand_mean.detach().cpu().numpy()
        hand_components = self.mano_model.hand_components.detach().cpu().numpy()
        
        # Subtract mean from our calculated pose
        centered_pose = full_pose_flat - hand_mean
        
        # Project onto top N PCA components
        # This filters out unnatural rotations (noise)
        pca_coeffs = centered_pose.dot(hand_components[:self.num_pca_comps].T)
        
        # Prepare Tensors
        # Hand Pose: The PCA coefficients
        hand_pose_tensor = torch.tensor(pca_coeffs, dtype=torch.float32).unsqueeze(0)
        # Global Orient: The wrist rotation
        global_orient_tensor = torch.tensor(wrist_rot_vec, dtype=torch.float32).unsqueeze(0)

        # Forward Pass
        output = self.mano_model(
            global_orient = global_orient_tensor,
            hand_pose     = hand_pose_tensor,
            betas         = torch.zeros([1, 10]),
            return_verts  = True
        )
        
        # Render
        vertices = output.vertices.detach().cpu().numpy().squeeze()
        vertices -= np.mean(vertices, axis=0) # Center mesh
        vertices *= 15.0 # Scale for viz

        self.viewer.render_lock.acquire()
        try:
            if self.hand_node:
                self.scene.remove_node(self.hand_node)
            new_mesh = pyrender.Mesh.from_trimesh(trimesh.Trimesh(vertices, self.mano_model.faces))
            self.hand_node = self.scene.add(new_mesh)
        finally:
            self.viewer.render_lock.release()



def main():
    cap = cv2.VideoCapture(0)

    hand_calculator = Hand3D()
    visualizer      = ManoHandVisualizer(MANO_MODEL_PATH)

    with HandTracker(model_path) as tracker:
        while cap.isOpened():
            (success, frame) = cap.read()
            if not success: break;

            frame = cv2.flip(frame, 1)
            tracker.detect(frame)
            result = hand_calculator.get_3d_coordinates(tracker.latest_result)
            visualizer.update(result)

    cap.release()
    cv2.destroyAllWindows()

    return;



if __name__ == "__main__":
    main()
