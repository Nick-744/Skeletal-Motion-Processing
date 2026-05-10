from datetime import datetime
from pathlib import Path
import open3d as o3d
import numpy as np



def reconstruct_surface_poisson(
        input_ply_path:   str,
        output_mesh_path: str,
        poisson_depth:    int   = 12,
        decimation_ratio: float = 0.1) -> None:
    ''' Reads a point cloud from a .ply file and outputs a 3D mesh using Poisson Surface Reconstruction. '''

    print(f'\n-> Loading point cloud from: {input_ply_path}')
    pcd = o3d.io.read_point_cloud(input_ply_path)
    
    if not pcd.has_points():
        raise ValueError(f'No points found in {input_ply_path}...');
    
    print(f'- Successfully loaded {len(pcd.points)} points.')

    # Estimate and orient normals
    if not pcd.has_normals():
        print('- Estimating normals...')
        pcd.estimate_normals(search_param = o3d.geometry.KDTreeSearchParamKNN(knn = 120))
        pcd.orient_normals_consistent_tangent_plane(k = 100)

    # Poisson surface reconstruction
    print(f'\n-> Running Poisson reconstruction (Depth: {poisson_depth})...')
    # Higher depth = More detail
    (mesh, densities) = o3d.geometry.TriangleMesh.create_from_point_cloud_poisson(pcd, depth = poisson_depth)

    # Filter low-density artifacts
    print('- Filtering extraneous geometry based on point density...')
    densities          = np.asarray(densities)
    vertices_to_remove = densities < np.quantile(densities, 0.01)
    mesh.remove_vertices_by_mask(vertices_to_remove)

    print('- Smoothing the mesh...')
    mesh = mesh.filter_smooth_taubin(number_of_iterations = 30)
    mesh.compute_vertex_normals()

    # Reduce number of triangles - Reduce file size!
    if decimation_ratio < 1.0:
        target_triangles = max(1, int(len(mesh.triangles) * decimation_ratio))
        print(f'- Decimating mesh from {len(mesh.triangles)} to {target_triangles} triangles...')
        mesh = mesh.simplify_quadric_decimation(target_number_of_triangles=target_triangles)
        mesh.compute_vertex_normals()

    # Export the Mesh
    print(f'\n-> Saving mesh to: {output_mesh_path}')
    o3d.io.write_triangle_mesh(output_mesh_path, mesh)

    print('- Mesh reconstruction complete!')

    return;



def main():
    # --- Configuration --- #
    file_name    = input('Enter the .ply file name: ').strip()
    DESKTOP_PATH = Path.home() / 'Desktop'

    INPUT_FILE = DESKTOP_PATH / file_name

    timestamp   = datetime.now().strftime('%Y%m%d_%H%M%S')
    OUTPUT_FILE = DESKTOP_PATH / f'reconstructed_mesh_{timestamp}.obj'

    if INPUT_FILE.exists():
        reconstruct_surface_poisson(INPUT_FILE, OUTPUT_FILE)
    else:
        print(f"ERROR: Could not find the input file '{INPUT_FILE}'.")
    
    return;



if __name__ == '__main__':
    main()
