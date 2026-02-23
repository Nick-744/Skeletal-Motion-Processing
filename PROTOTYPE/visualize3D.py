import cv2
import matplotlib.pyplot as plt

from mediapipeHLD import (HandTracker, model_path, mp_hands)



class HandVisualizer3D:
    def __init__(self, max_hands = 2):
        self.max_hands        = max_hands
        self.connections_list = list(mp_hands.HAND_CONNECTIONS)
        
        plt.ion() # Interactive mode
        self.fig = plt.figure(figsize = (8, 8))
        
        # Setup 3D Subplot
        self.ax = self.fig.add_subplot(111, projection = '3d')
        self.setup_plot_layout()

        # Scatter plots (Joints)
        self.scatters = [
            self.ax.scatter([], [], [], s = 40, c = color, depthshade = False)
            for color in ['#FF0000', '#0000FF'] # Red (Hand 1st), Blue (Hand 2nd)
        ]
        
        # Line plots (Bones) - List of lists [hand_idx][bone_idx]
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
        self.ax.set_xlim(-0.1, 0.1)
        self.ax.set_ylim(-0.1, 0.1)
        self.ax.set_zlim(-0.1, 0.1)

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

    def update(self, result: HandTracker.latest_result) -> None:
        ''' Updates the plot with new landmark data. '''
        
        # If no result, clear the plot...
        if not result:
            self._clear_plot()
            return;

        # Loop through detected hands
        for (i, landmarks) in enumerate(result.hand_world_landmarks):
            if i >= self.max_hands: break;

            # Extract Coordinates (hand_landmarks Vs. hand_world_landmarks???)
            xs = [lm.x for lm in landmarks]
            ys = [lm.y for lm in landmarks]
            zs = [lm.z for lm in landmarks] 

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
        for j in range(len(result.hand_landmarks), self.max_hands):
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

    max_hands = 2
    visualizer = HandVisualizer3D(max_hands = max_hands)

    with HandTracker(model_path, num_hands = max_hands) as tracker:
        while True:
            (success, frame) = cap.read()
            if not success: break;

            frame = cv2.flip(frame, 1)
            tracker.detect(frame)
            result = tracker.latest_result

            if visualizer.is_active(): visualizer.update(result)

            if cv2.waitKey(1) & 0xFF == 27: break;

    cap.release()
    cv2.destroyAllWindows()
    plt.close()

    return;



if __name__ == '__main__':
    main()
