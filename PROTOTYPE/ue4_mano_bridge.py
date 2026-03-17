import open3d as o3d
import numpy as np
import os



# ---< Paths >--- #
current_dir = os.path.dirname(os.path.realpath(__file__))

temp_path      = os.path.join(current_dir, 'ASSETS', '3D_models')
left_fbx_path  = os.path.join(temp_path, 'MANO_left.fbx')
right_fbx_path = os.path.join(temp_path, 'MANO_right.fbx')



class UE4ManoVisualizer:
    def __init__(self, left_fbx_path: str, right_fbx_path: str):
        self.vis = o3d.visualization.Visualizer()
        self.vis.create_window(window_name = 'UE4 Emulated Coordinate System', width = 1280, height = 720)
        
        # UE4 Transformation Matrix (RHS Y-Up -> LHS Z-Up)
        # X = Forward - Y = Right - Z = Up (UE4)
        self.ue4_transform = np.array([
            [-1,  0, 0, 0],
            [ 0,  0, 1, 0],
            [ 0, -1, 0, 0],
            [ 0,  0, 0, 1]
        ])

        # Load the FBX meshes for left and right hands
        self.left_mesh  = self._load_fbx(left_fbx_path,  [0.8, 0.6, 0.6])
        self.right_mesh = self._load_fbx(right_fbx_path, [0.6, 0.6, 0.8])
        self.vis.add_geometry(self.left_mesh)
        self.vis.add_geometry(self.right_mesh)

        # The x, y, z axis will be rendered as red, green, and blue arrows respectively
        self.axis_frame = o3d.geometry.TriangleMesh.create_coordinate_frame(size = 20.0)
        self.axis_frame.transform(self.ue4_transform)
        self.vis.add_geometry(self.axis_frame)

        # Adjust Camera
        view_ctl = self.vis.get_view_control()
        view_ctl.set_front([1, 0, 0])

        return;

    def _load_fbx(self, path: str, color: list) -> o3d.geometry.TriangleMesh:
        mesh = o3d.t.io.read_triangle_mesh(path, enable_post_processing = True)

        mesh = mesh.to_legacy()
        mesh.compute_vertex_normals()
        mesh.paint_uniform_color(color)

        return mesh;

    def update(self, left_verts, right_verts, left_anchor, right_anchor):
        ''' Applies UE4 transformation to the raw MANO vertices. '''

        # Apply the anchor and transformation to Left Hand
        self.left_mesh.vertices = o3d.utility.Vector3dVector(left_verts + left_anchor)
        self.left_mesh.transform(self.ue4_transform)
        
        # Apply the anchor and transformation to Right Hand
        self.right_mesh.vertices = o3d.utility.Vector3dVector(right_verts + right_anchor)
        self.right_mesh.transform(self.ue4_transform)

        # Update the visualizer...
        self.vis.update_geometry(self.left_mesh)
        self.vis.update_geometry(self.right_mesh)
        self.vis.poll_events()
        self.vis.update_renderer()

        return;

def main():
    visualizer = UE4ManoVisualizer(left_fbx_path, right_fbx_path)
    visualizer.vis.run()

    return;

if __name__ == '__main__':
    main()
