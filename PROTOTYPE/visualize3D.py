import cv2
import matplotlib.pyplot as plt

from mediapipeHLD import (HandTracker, model_path, mp_hands)

from typing import TypeAlias
HandData: TypeAlias = list[tuple[list[float], list[float], list[float]]]



class Hand3D:
    def __init__(self): pass;

    def get_3d_coordinates(self,
        result: HandTracker.latest_result) -> HandData:
        ''' Transforms MediaPipe's hand landmarks into 3D coordinates. '''

        hands_data = []

        if (not result) or (not result.hand_landmarks): return hands_data;

        # Loop through detected hands
        # - Note: result.hand_world_landmarks contains the accurate metric shapes!
        for (i, world_landmarks) in enumerate(result.hand_world_landmarks):
            # Define the anchor (Wrist - index 0)
            screen_landmarks = result.hand_landmarks[i]
            anchor_x = screen_landmarks[0].x
            anchor_y = screen_landmarks[0].y
            anchor_z = self._estimate_scale(screen_landmarks, world_landmarks)
            scale    = 3.0

            # Translate World Landmarks to the Screen Space anchor
            xs = [lm.x * scale + anchor_x for lm in world_landmarks]
            ys = [lm.y * scale + anchor_y for lm in world_landmarks]
            zs = [lm.z * scale + anchor_z for lm in world_landmarks]

            hands_data.append((xs, ys, zs))
        
        return hands_data;

    def _estimate_scale(self, screen_lms, world_lms) -> float:
        '''
        Estimate the World2Screen scale factor by comparing bone lengths!
        Closer hand -> larger screen-space distances -> larger scale.
        Uses a stable subset of bones for robustness.
        Works well for depth estimation...
        '''

        # Indices of connections to sample for scale estimation...
        SAMPLE_CONNECTIONS = [
            (0, 1), (1,  2), ( 2,  3), (3, 4), # thumb
            (0, 5), (5,  6), ( 0, 17),         # index base, pinky base
            (5, 9), (9, 13), (13, 17),         # knuckle row
        ]

        screen_lengths = []
        world_lengths  = []
        for (s, e) in SAMPLE_CONNECTIONS:
            # Screen-space 2D length (XY only - not accurate metric)
            dx_s = screen_lms[s].x - screen_lms[e].x
            dy_s = screen_lms[s].y - screen_lms[e].y
            screen_lengths.append((dx_s*dx_s + dy_s*dy_s) ** 0.5)

            # World-space 3D length (accurate metric proportions)
            dx_w = world_lms[s].x - world_lms[e].x
            dy_w = world_lms[s].y - world_lms[e].y
            dz_w = world_lms[s].z - world_lms[e].z
            world_lengths.append((dx_w*dx_w + dy_w*dy_w + dz_w*dz_w) ** 0.5)

        mean_screen = sum(screen_lengths) / len(screen_lengths)
        mean_world  = sum(world_lengths)  / len(world_lengths)

        return mean_screen / mean_world if mean_world > 1e-6 else 1.0;



class HandVisualizer3D:
    ''' Responsible ONLY for drawing the Hand3D data using Matplotlib. '''

    def __init__(self, max_hands: int = 2):
        self.max_hands        = max_hands
        self.connections_list = list(mp_hands.HAND_CONNECTIONS)
        
        plt.ion() # Interactive mode
        self.fig = plt.figure(figsize = (8, 8))
        
        # Setup 3D Subplot
        self.ax = self.fig.add_subplot(111, projection = '3d')
        self.setup_plot_layout()

        # Init scatter plots (Joints)
        self.scatters = [
            self.ax.scatter([], [], [], s = 40, c = color, depthshade = False)
            for color in ['#FF0000', '#0000FF'] # Red (Hand 1st), Blue (Hand 2nd)
        ]
        
        # Init line plots (Bones) - List of lists [hand_idx][bone_idx]
        self.bones = []
        for i in range(max_hands):
            hand_lines = []
            color      = 'red' if i == 0 else 'blue'
            for _ in range(len(self.connections_list)):
                line, = self.ax.plot([], [], [], color = color, linewidth = 2)
                hand_lines.append(line)
            self.bones.append(hand_lines)
        
        return;

    def setup_plot_layout(self) -> None:
        ''' Configures the 3D axis. '''

        # Z points away - X horizontal - Y vertical
        self.ax.view_init(elev = -90, azim = -90)
        
        # Axis Limits
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)
        self.ax.set_zlim(2, 6) # Trial-and-error range...

        # Equal axis lengths
        self.ax.set_box_aspect([1, 1, 1])
        
        # Labels
        self.ax.set_xlabel('X')
        self.ax.set_ylabel('Y')
        self.ax.set_zlabel('Relative Z')
        self.ax.set_title('< 3D Hand Visualization >')

        self.ax.set_xticklabels([])
        self.ax.set_yticklabels([])
        self.ax.set_zticklabels([])
        
        return;

    def update(self, hands_data: HandData) -> None:
        ''' Updates the plot with new landmark data. '''
        
        # If no result, clear the plot...
        if not hands_data:
            self._clear_plot()
            return;

        for (i, (xs, ys, zs)) in enumerate(hands_data):
            # Update Joints (Scatter)
            self.scatters[i]._offsets3d = (xs, ys, zs)

            # Update Bones (Lines)
            for (line_idx, connection) in enumerate(self.connections_list):
                start = connection.start
                end   = connection.end
                
                # Create coordinate pairs for the line
                lx = [xs[start], xs[end]]
                ly = [ys[start], ys[end]]
                lz = [zs[start], zs[end]]

                self.bones[i][line_idx].set_data(lx, ly)
                self.bones[i][line_idx].set_3d_properties(lz)

        # Clear
        for j in range(len(hands_data), self.max_hands):
            self._clear_single_hand(j)

        self.fig.canvas.draw_idle()
        self.fig.canvas.flush_events()

        return;

    # ---< Helper Methods >--- #
    def _clear_plot(self) -> None:
        for i in range(self.max_hands): self._clear_single_hand(i)
        
        self.fig.canvas.draw_idle()
        self.fig.canvas.flush_events()

        return;

    def _clear_single_hand(self, index: int) -> None:
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
    hand_calculator = Hand3D()
    # Responsible for visualizing the 3D coordinates using Matplotlib!
    visualizer      = HandVisualizer3D()

    with HandTracker(model_path) as tracker:
        while True:
            (success, frame) = cap.read()
            if not success: break;

            # Exit if visualizer window is closed
            if not visualizer.is_active(): break;

            frame  = cv2.flip(frame, 1)
            tracker.detect(frame)
            result = hand_calculator.get_3d_coordinates(tracker.latest_result)
            visualizer.update(result)

    cap.release()
    cv2.destroyAllWindows()
    plt.close()

    return;



if __name__ == '__main__':
    main()
