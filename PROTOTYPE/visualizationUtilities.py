import cv2
import numpy as np
import open3d as o3d

from mediapipeHLD import HandTracker, model_path

import MINIMAL_IK.config as config
from MINIMAL_IK.models import KinematicModel, KinematicPCAWrapper
from MINIMAL_IK.solver import Solver
from MINIMAL_IK.armatures import MANOArmature

# Maps MANO keypoint indices to MediaPipe landmark indices
# Based on MANOArmature.labels: W, I0-2, M0-2, L0-2, R0-2, T0-2, I3, M3, L3, R3, T3
MP_TO_MANO_MAPPING = [
    0,   # 0: Wrist ('W')
    5,   # 1: Index MCP ('I0')
    6,   # 2: Index PIP ('I1')
    7,   # 3: Index DIP ('I2')
    9,   # 4: Middle MCP ('M0')
    10,  # 5: Middle PIP ('M1')
    11,  # 6: Middle DIP ('M2')
    17,  # 7: Pinky MCP ('L0')
    18,  # 8: Pinky PIP ('L1')
    19,  # 9: Pinky DIP ('L2')
    13,  # 10: Ring MCP ('R0')
    14,  # 11: Ring PIP ('R1')
    15,  # 12: Ring DIP ('R2')
    1,   # 13: Thumb CMC ('T0')
    2,   # 14: Thumb MCP ('T1')
    3,   # 15: Thumb IP  ('T2')
    8,   # 16: Index Tip ('I3') - Extended
    12,  # 17: Middle Tip ('M3') - Extended
    20,  # 18: Pinky Tip ('L3') - Extended
    16,  # 19: Ring Tip ('R3') - Extended
    4    # 20: Thumb Tip ('T3') - Extended
]

def run_realtime_visualization():
    # 1. Initialize Kinematic Model & Solver
    n_pose = 12 
    n_shape = 10 
    
    mesh_model = KinematicModel(config.MANO_MODEL_PATH, MANOArmature, scale=1.0)
    wrapper = KinematicPCAWrapper(mesh_model, n_pose=n_pose)
    solver = Solver(max_iter=3, verbose=False)
    params_est = np.zeros(wrapper.n_params)

    # --- NEW: Get default resting keypoints to anchor the other fingers ---
    _, default_keypoints = mesh_model.set_params(
        pose_pca=np.zeros(n_pose), 
        shape=np.zeros(n_shape), 
        pose_glb=np.zeros((1, 3))
    )
    default_keypoints -= default_keypoints[0] # Align wrist to origin

    # 2. Setup Open3D Visualizer
    vis = o3d.visualization.Visualizer()
    vis.create_window(window_name="Real-Time MANO IK", width=800, height=600)
    
    o3d_mesh = o3d.geometry.TriangleMesh()
    o3d_mesh.vertices = o3d.utility.Vector3dVector(mesh_model.verts)
    o3d_mesh.triangles = o3d.utility.Vector3iVector(mesh_model.faces)
    o3d_mesh.compute_vertex_normals()
    o3d_mesh.paint_uniform_color([0.7, 0.7, 0.7]) 
    vis.add_geometry(o3d_mesh)

    # 3. Initialize Webcam and MediaPipe
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): 
        raise RuntimeError("Could not open webcam.")
        
    with HandTracker(model_path) as tracker:
        while True:
            success, frame = cap.read()
            if not success: 
                break
                
            frame = cv2.flip(frame, 1) # Selfie view
            tracker.detect(frame)
            result = tracker.latest_result
            
            if result and result.hand_world_landmarks:
                landmarks = result.hand_world_landmarks[0]
                
                # 1. Extract ALL 21 MediaPipe landmarks
                mp_keypoints = np.array([[lm.x, lm.y, lm.z] for lm in landmarks])
                
                # 2. CRUCIAL FIX: Flip Y and Z axes to match MANO's coordinate system
                mp_keypoints[:, 1] *= -1 
                mp_keypoints[:, 2] *= -1 
                
                # 3. Reorder to match MANO's expected layout
                target_keypoints = mp_keypoints[MP_TO_MANO_MAPPING]
                
                # 4. Align roots: shift target keypoints so the wrist is at origin
                target_keypoints -= target_keypoints[0]
                
                # -----------------------------------------------------------------
                # 5. DYNAMIC SCALE MATCHING
                # Calculate the length of the base index bone (Wrist to Index MCP)
                mano_base_dist = np.linalg.norm(default_keypoints[1] - default_keypoints[0])
                mp_base_dist = np.linalg.norm(target_keypoints[1] - target_keypoints[0])
                
                # Scale MediaPipe points so proportions match MANO's physical size
                scale_factor = mano_base_dist / (mp_base_dist + 1e-8)
                target_keypoints *= scale_factor
                # -----------------------------------------------------------------
                
                # 6. --- The Blending Trick ---
                # We only want the index finger and wrist to move.
                # Based on MANOArmature.labels: W(0), I0(1), I1(2), I2(3), I3(16)
                index_indices = [0, 1, 2, 3, 16]
                
                blended_target = default_keypoints.copy()
                blended_target[index_indices] = target_keypoints[index_indices]
                
                # 7. Solve IK using the blended target
                params_est = solver.solve(wrapper, blended_target)
                
                # 8. Decode parameters and update the Kinematic Model
                shape_est, pose_pca_est, pose_glb_est = wrapper.decode(params_est)
                
                # -----------------------------------------------------------------
                # 9. FREEZE SHAPE
                # Force the shape to remain the default human proportion, ignoring 
                # any mutations the solver tried to apply to reach the target.
                shape_est = np.zeros(n_shape)
                # -----------------------------------------------------------------

                verts, _ = mesh_model.set_params(
                    pose_pca=pose_pca_est, 
                    pose_glb=pose_glb_est, 
                    shape=shape_est
                )
                
                # Push updated vertices to Open3D
                o3d_mesh.vertices = o3d.utility.Vector3dVector(verts)
                o3d_mesh.compute_vertex_normals()
                vis.update_geometry(o3d_mesh)
            
            # Non-blocking render calls
            vis.poll_events()
            vis.update_renderer()
            
            tracker.draw(frame)
            cv2.imshow("Webcam Feed", frame)
            
            if (cv2.waitKey(1) & 0xFF == 27) or cv2.getWindowProperty("Webcam Feed", cv2.WND_PROP_VISIBLE) < 1:
                break
                
    cap.release()
    cv2.destroyAllWindows()
    vis.destroy_window()

if __name__ == '__main__':
    run_realtime_visualization()
    