import cv2
import trimesh
import pyrender
import numpy as np

from MINIMAL_IK.models import KinematicModel, KinematicPCAWrapper
from MINIMAL_IK.solver import Solver
from MINIMAL_IK.armatures import MANOArmature
from MINIMAL_IK.config import MANO_MODEL_PATH

from mediapipeHLD import (HandTracker, model_path)
from visualize3D import (HandDataRaw, Hand3D)

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
        # 1. Initialize MINIMAL_IK Components
        self.kinematic_model = KinematicModel(model_path, MANOArmature)
        
        # We use 15 PCA components for pose (plus 3 global rotation, 10 shape)
        self.wrapper = KinematicPCAWrapper(self.kinematic_model, n_pose=15)
        
        # Initialize the IK Solver
        # max_iter is kept relatively low (e.g., 5-10) for real-time performance
        self.solver = Solver(max_iter=8, verbose=False)
        
        # Store previous params to use as the initial guess for the next frame (temporal smoothing)
        self.current_params = np.zeros(self.wrapper.n_params)

        # 2. Map MediaPipe indices (0-20) to MANOArmature indices
        # MP order: Wrist, Thumb(4), Index(4), Middle(4), Ring(4), Pinky(4)
        # MANO order: W, I0-2, M0-2, L0-2(Pinky), R0-2(Ring), T0-2(Thumb), Ext(I3, M3, L3, R3, T3)
        self.mp_to_mano_idx = [
            0,             # W (Wrist)
            5, 6, 7,       # Index (I0, I1, I2)
            9, 10, 11,     # Middle (M0, M1, M2)
            17, 18, 19,    # Pinky (L0, L1, L2)
            13, 14, 15,    # Ring (R0, R1, R2)
            1, 2, 3,       # Thumb (T0, T1, T2)
            8,             # Index Tip (I3)
            12,            # Middle Tip (M3)
            20,            # Pinky Tip (L3)
            16,            # Ring Tip (R3)
            4              # Thumb Tip (T3)
        ]

        # 3. Setup Pyrender Scene
        self.scene  = pyrender.Scene(bg_color = [0.1, 0.1, 0.1])
        light       = pyrender.DirectionalLight(color = [1.0, 1.0, 1.0], intensity = 3.0)
        self.scene.add(light, pose = np.eye(4))
        
        self.viewer = pyrender.Viewer(
            self.scene,
            run_in_thread        = True,
            use_raymond_lighting = True
        )
        self.hand_node = None

        return;

    def update(self, hands_data: HandDataRaw) -> None:
        if not hands_data: return

        # Extract 3D coordinates from MediaPipe result
        (xs, ys, zs) = hands_data[0]
        mp_kpts = np.vstack((xs, ys, zs)).T  # Shape: (21, 3)

        # Reorder MediaPipe keypoints to match the MANOArmature structure
        target_kpts = mp_kpts[self.mp_to_mano_idx]

        # Center the target keypoints around the wrist (root joint)
        # MINIMAL_IK optimizes rotations, so global translation must be zeroed out
        root_translation = target_kpts[0].copy()
        target_kpts -= root_translation

        # Run Inverse Kinematics solver
        # Using previous frame's params as `init` gives a massive speedup and reduces jitter
        self.current_params = self.solver.solve(
            self.wrapper, 
            target=target_kpts, 
            init=self.current_params
        )

        # Decode the solved parameters and update the kinematic model
        shape, pose_pca, pose_glb = self.wrapper.decode(self.current_params)
        vertices, _ = self.kinematic_model.set_params(
            pose_glb=pose_glb, 
            pose_pca=pose_pca, 
            shape=shape
        )

        # Render the updated mesh
        # Optionally re-apply the root translation if you want the hand to move in 3D space:
        # vertices += root_translation 
        
        # Note: Depending on your Hand3D scale vs MANO scale, you may need to multiply vertices.
        # Assuming Hand3D outputs coordinates similar to MANO meters scale.
        
        self.viewer.render_lock.acquire()
        try:
            if self.hand_node:
                self.scene.remove_node(self.hand_node)
            
            # Reconstruct trimesh and add to scene
            new_mesh = pyrender.Mesh.from_trimesh(
                trimesh.Trimesh(vertices, self.kinematic_model.faces)
            )
            self.hand_node = self.scene.add(new_mesh)
        finally:
            self.viewer.render_lock.release()

        return;



def main():
    cap = cv2.VideoCapture(0)

    hand_calculator = Hand3D()
    visualizer      = ManoHandVisualizer(MANO_MODEL_PATH)

    with HandTracker(model_path) as tracker:
        while cap.isOpened():
            (success, frame) = cap.read()
            if not success: break;

            frame  = cv2.flip(frame, 1)
            tracker.detect(frame)
            result = hand_calculator.get_3d_coordinates(tracker.latest_result)
            visualizer.update(result[0])

    cap.release()
    cv2.destroyAllWindows()

    return;



if __name__ == "__main__":
    main()
