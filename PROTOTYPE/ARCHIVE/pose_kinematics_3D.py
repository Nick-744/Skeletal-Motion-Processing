import cv2
import numpy as np
import matplotlib.pyplot as plt
from MANOkinematics.smoother import OneEuroFilter

from pose_engine import (PoseTracker, model_path)

from typing import TypeAlias
from numpy.typing import NDArray
PoseDataRaw: TypeAlias = list[tuple[NDArray[np.float64], NDArray[np.float64], NDArray[np.float64]]]



class Pose3D:
    def __init__(self, mincutoff: float = 2.0, beta: float = 0.05):
        self.pose_filters = OneEuroFilter(mincutoff = mincutoff, beta = beta)

        # Filter (Data Reduction)
        self.target_indices = [
            0,  # Head - nose
            7,  # Head - left ear
            8,  # Head - right ear
            11, # ArmL - left shoulder
            13, # ArmL - left elbow
            12, # ArmR - right shoulder
            14, # ArmR - right elbow
            23, # LegL - left hip
            25, # LegL - left knee
            24, # LegR - right hip
            26  # LegR - right knee
        ]

        return;

    def get_3d_coordinates(self,
        result: PoseTracker.latest_result) -> PoseDataRaw:
        '''
        Transforms MediaPipe's pose landmarks into 3D coordinates.
        
        Returns
        -------
        A list of 1 tuple containing 3 numpy arrays (xs, ys, zs)
        for the 11 target landmarks in world coordinates.
        '''

        poses_data = []

        if (not result) or (not result.pose_landmarks): return poses_data;

        # Grab ONLY the first person (index 0)...
        # - Note: result.pose_world_landmarks contains the accurate metric shapes!
        world_landmarks = result.pose_world_landmarks[0]

        # Pose shape in World Coordinates (only the 11 target points)...
        raw_coords      = np.array([
            [world_landmarks[idx].x, world_landmarks[idx].y, world_landmarks[idx].z]
            for idx in self.target_indices
        ])
        filtered_coords = self.pose_filters.process(raw_coords)
        (xw, yw, zw)    = (filtered_coords[:, 0], filtered_coords[:, 1], filtered_coords[:, 2])

        poses_data.append((xw, yw, zw))

        return poses_data;

    def _estimate_scale(self, screen_lms, world_lms) -> float:
        '''
        Estimate the World2Screen scale factor by comparing bone lengths!
        Closer pose -> larger screen-space distances -> larger scale.
        Uses a stable subset of bones for robustness.
        Works well for depth estimation...
        '''

        # Mid shoulder (11, 12) to Mid hip (23, 24)
        ssl_x = (screen_lms[11].x + screen_lms[12].x) / 2 # Screen Shoulder Line/Midpoint
        ssl_y = (screen_lms[11].y + screen_lms[12].y) / 2
        shl_x = (screen_lms[23].x + screen_lms[24].x) / 2 # Screen Hip Line/Midpoint
        shl_y = (screen_lms[23].y + screen_lms[24].y) / 2
        
        screen_len = (
            (ssl_x - shl_x) * (ssl_x - shl_x) +
            (ssl_y - shl_y) * (ssl_y - shl_y)
        ) ** 0.5

        wsl_x = (world_lms[11].x + world_lms[12].x) / 2 # World Shoulder Line/Midpoint
        wsl_y = (world_lms[11].y + world_lms[12].y) / 2
        wsl_z = (world_lms[11].z + world_lms[12].z) / 2
        whl_x = (world_lms[23].x + world_lms[24].x) / 2 # World Hip Line/Midpoint
        whl_y = (world_lms[23].y + world_lms[24].y) / 2
        whl_z = (world_lms[23].z + world_lms[24].z) / 2
        
        world_len = (
            (wsl_x - whl_x) * (wsl_x - whl_x) +
            (wsl_y - whl_y) * (wsl_y - whl_y) +
            (wsl_z - whl_z) * (wsl_z - whl_z)
        ) ** 0.5

        return screen_len / world_len if world_len > 1e-6 else 1.0;



class PoseVisualizer3D:
    ''' Responsible ONLY for drawing the 11 Pose3D points using Matplotlib. '''

    def __init__(self):
        # Based on self.target_indices in Pose3D...
        self.connections = [
            # Head Triangle (Nose - L Ear - R Ear)
            (0, 1), (0, 2), (1, 2),

            # Limbs
            (3, 4), # Left Arm  (Shoulder -> Elbow)
            (5, 6), # Right Arm (Shoulder -> Elbow)
            (7, 8), # Left Leg  (Hip -> Knee)
            (9, 10) # Right Leg (Hip -> Knee)
        ]
        
        plt.ion() # Interactive mode
        self.fig = plt.figure(figsize = (8, 8))

        # Setup 3D Subplot
        self.ax = self.fig.add_subplot(111, projection = '3d')
        self.setup_plot_layout()

        # Init scatter plots (Joints)
        self.scatters = [
            self.ax.scatter([], [], [], s = 40, c = 'green', depthshade = False)
        ]
        
        # Init line plots (Bones) - List of lists [pose_idx][bone_idx]
        self.bones = []
        pose_lines = []
        for _ in range(len(self.connections)):
            line, = self.ax.plot([], [], [], color = 'blue', linewidth = 2)
            pose_lines.append(line)
        self.bones.append(pose_lines)

        return;

    def setup_plot_layout(self) -> None:
        ''' Configures the 3D axis. '''

        # Z points away - X horizontal - Y vertical
        self.ax.view_init(elev = -90, azim = -90)

        # Axis Limits
        self.ax.set_xlim(-0.5, 0.5)
        self.ax.set_ylim(-0.5, 0.5)
        self.ax.set_zlim(-0.5, 0.5)

        # Equal axis lengths
        self.ax.set_box_aspect([1, 1, 1])

        # Labels
        self.ax.set_xlabel('X')
        self.ax.set_ylabel('Y')
        self.ax.set_zlabel('Relative Z')
        self.ax.set_title('< 3D Pose Visualization >')

        self.ax.set_xticklabels([])
        self.ax.set_yticklabels([])
        self.ax.set_zticklabels([])

        return;

    def update(self, poses_data: PoseDataRaw) -> None:
        ''' Updates the plot with new landmark data. '''
        
        # If no result, clear the plot...
        if len(poses_data) == 0:
            self._clear_plot()
            return;

        for (i, (xs, ys, zs)) in enumerate(poses_data):
            # Update Joints (Scatter)
            self.scatters[i]._offsets3d = (xs, ys, zs)

            for (line_idx, (start, end)) in enumerate(self.connections):
                lx = [xs[start], xs[end]]
                ly = [ys[start], ys[end]]
                lz = [zs[start], zs[end]]

                self.bones[i][line_idx].set_data(lx, ly)
                self.bones[i][line_idx].set_3d_properties(lz)

        # Clear
        for j in range(len(poses_data), 1): # Only 1 pose...
            self._clear_single_pose(j)

        self.fig.canvas.draw_idle()
        self.fig.canvas.flush_events()

        return;

    # ---< Helper Methods >--- #
    def _clear_plot(self) -> None:
        self._clear_single_pose(0)
        self.fig.canvas.draw_idle()
        self.fig.canvas.flush_events()

        return;

    def _clear_single_pose(self, index: int) -> None:
        self.scatters[index]._offsets3d = ([], [], [])
        for line in self.bones[index]:
            line.set_data([], [])
            line.set_3d_properties([])
        
        return;

    def is_active(self): return plt.fignum_exists(self.fig.number);



def main():
    # Initialize Webcam
    cap = cv2.VideoCapture(0)
    if not cap.isOpened(): raise RuntimeError('Could not open webcam.');

    # Responsible for converting MediaPipe results into 3D coordinates!
    pose_calculator = Pose3D()
    # Responsible for visualizing the 3D coordinates using Matplotlib!
    visualizer      = PoseVisualizer3D()

    with PoseTracker(model_path) as tracker:
        while True:
            (success, frame) = cap.read()
            if not success: break;
            
            # Exit if visualizer window is closed
            if not visualizer.is_active(): break;

            frame  = cv2.flip(frame, 1)
            tracker.detect(frame)
            result = pose_calculator.get_3d_coordinates(tracker.latest_result)
            visualizer.update(result)

    cap.release()
    cv2.destroyAllWindows()
    plt.close()

    return;



if __name__ == '__main__':
    main()
