# MIT License
#
# Copyright (c) 2020 Yuxiao Zhou
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.

import numpy as np
import pickle



class KinematicModel():
  '''
  Kinematic model that takes in model parameters and outputs mesh, keypoints, etc.
  '''

  def __init__(self, model_path: str, armature: object, scale: int = 1, apply_pose_blend_shapes: bool = False):
    '''
    Parameters
    ----------
    model_path : str
      Path to the model to be loaded.
    armature   : object
      An armature class from `armatures.py`.
    scale      : int, optional
      Scale of the model to make the solving easier, by default 1
    
    apply_pose_blend_shapes : bool, optional
      Whether to apply pose blend shapes, by default False
    '''

    with open(model_path, 'rb') as f:
      params = pickle.load(f)

      # Load PCA components for pose (used to reduce the dimensionality of the pose space)
      self.pose_pca_basis = params['pose_pca_basis']
      self.pose_pca_mean  = params['pose_pca_mean' ]

      # Load the joint regressor matrix (calculates joint 3D locations from mesh vertices)
      self.J_regressor = params['J_regressor']

      # Load skinning weights (defines how much each joint's movement affects each vertex)
      self.skinning_weights = params['skinning_weights']

      self.mesh_pose_basis  = params['mesh_pose_basis' ] # Pose blend shape
      self.mesh_shape_basis = params['mesh_shape_basis']
      self.mesh_template    = params['mesh_template'   ] # T-pose

      self.faces =  params['faces']

      self.parents = params['parents'] # Kinematic tree

    self.n_shape_params = self.mesh_shape_basis.shape[-1]
    self.scale          = scale

    self.armature = armature
    self.n_joints = self.armature.n_joints

    self.apply_pose_blend_shapes = apply_pose_blend_shapes

    self.pose  = np.zeros((self.n_joints, 3))
    self.shape = np.zeros(self.mesh_shape_basis.shape[-1])

    self.verts     = None
    self.J         = None
    self.R         = None
    self.keypoints = None

    # Create an extended joint regressor to calculate extra keypoints (fingertips)
    self.J_regressor_ext = np.zeros([self.armature.n_keypoints, self.J_regressor.shape[1]])
    self.J_regressor_ext[:self.armature.n_joints] = self.J_regressor
    for (i, v) in enumerate(self.armature.keypoints_ext):
      self.J_regressor_ext[i + self.armature.n_joints, v] = 1

    self.update() # Run the Forward Kinematics and Linear Blend Skinning...

    return;

  def set_params(self,
      pose_abs: np.ndarray = None,
      pose_pca: np.ndarray = None,
      pose_glb: np.ndarray = None,
      shape:    np.ndarray = None) -> tuple:
    '''
    Set model parameters and get the mesh. Do not set `pose_abs` and `pose_pca`
    at the same time.

    Parameters
    ----------
    pose_abs : np.ndarray, shape [n_joints, 3], optional
      The absolute model pose in axis-angle, by default None
    pose_pca : np.ndarray, optional
      The PCA coefficients of the pose, shape [n_pose, 3], by default None
    pose_glb : np.ndarray, shape [1, 3], optional
      Global rotation for the model, by default None
    shape    : np.ndarray, shape [n_shape], optional
      Shape coefficients of the pose, by default None

    Returns
    -------
    np.ndarray, shape [N, 3]
      Vertices coordinates of the mesh, scale applied.
    np.ndarray, shape [K, 3]
      Keypoints coordinates of the model, scale applied.
    '''

    # If an absolute axis-angle pose is provided...
    if pose_abs is not None:
      self.pose = pose_abs
    
    # Otherwise, if PCA pose coefficients are provided...
    elif pose_pca is not None:
      # Reconstruct the absolute pose from PCA
      self.pose = np.dot(
        np.expand_dims(pose_pca, 0), self.pose_pca_basis[:pose_pca.shape[0]]
      )[0] + self.pose_pca_mean
      self.pose = np.reshape(self.pose, [self.n_joints - 1, 3])

      if pose_glb is None: pose_glb = np.zeros([1, 3])
      pose_glb  = np.reshape(pose_glb, [1, 3])
      self.pose = np.concatenate([pose_glb, self.pose], 0)

    # If shape parameters (beta) are provided...
    if shape is not None: self.shape = shape

    return self.update();

  def update(self) -> tuple:
    '''
    Re-compute vertices and keypoints with given parameters.
    Forward Kinematics and Linear Blend Skinning!

    Returns
    -------
    np.ndarray, shape [N, 3]
      Vertices coordinates of the mesh, scale applied.
    np.ndarray, shape [K, 3]
      Keypoints coordinates of the model, scale applied.
    '''

    # Apply shape coefficients to the base template
    verts = self.mesh_template + self.mesh_shape_basis.dot(self.shape)

    self.J = self.J_regressor.dot(verts)
    self.R = self.rodrigues(self.pose.reshape((-1, 1, 3)))

    # POSE BLEND SHAPES: Add pose-corrective term B_P(theta)
    # For each non-root joint, flatten (R_n - I) into a 135-element feature vector
    # and project through the learned basis to get per-vertex corrections.
    if self.apply_pose_blend_shapes:
      pose_feature = (self.R[1:] - np.eye(3)).flatten()
      verts       += self.mesh_pose_basis.dot(pose_feature)

    # FORWARD KINEMATICS: Compute global transformation matrices for each joint
    G    = np.empty((self.n_joints, 4, 4))
    # Root joint (index 0):
    G[0] = self.with_zeros(np.hstack((self.R[0], self.J[0, :].reshape([3, 1]))))

    # Iterate through the rest of the joints
    for i in range(1, self.n_joints):
      # Global Transform = Parent Global Transform * Local Transform
      G[i] = G[self.parents[i]].dot(self.with_zeros(
          np.hstack([
            self.R[i],
            (self.J[i, :] - self.J[self.parents[i], :]).reshape([3, 1])
          ])
      ))

    # Deformation required to move vertices from the T-pose
    G = G - self.pack(np.matmul(
        G,
        np.hstack([self.J, np.zeros([self.n_joints, 1])]).reshape([self.n_joints, 4, 1])
    ))

    # LINEAR BLEND SKINNING (LBS)
    T = np.tensordot(self.skinning_weights, G, axes = [[1], [0]])

    verts = np.hstack((verts, np.ones([verts.shape[0], 1])))

    self.verts     = np.matmul(T, verts.reshape([-1, 4, 1])).reshape([-1, 4])[:, :3]
    self.keypoints = self.J_regressor_ext.dot(self.verts)

    # SCALING
    self.verts     *= self.scale
    self.keypoints *= self.scale

    return (self.verts.copy(), self.keypoints.copy());

  # ---< Helper functions >--- #

  def rodrigues(self, r: np.ndarray) -> np.ndarray:
    '''
    Rodrigues' rotation formula that turns axis-angle vector into rotation
    matrix in a batch-ed manner.

    Parameter:
    ----------
    r: Axis-angle rotation vector of shape [batch_size, 1, 3].

    Return:
    -------
    Rotation matrix of shape [batch_size, 3, 3].
    '''

    theta = np.linalg.norm(r, axis = (1, 2), keepdims = True)
    theta = np.maximum(theta, np.finfo(np.float64).eps) # Avoid zero divide

    r_hat   = r / theta
    cos     = np.cos(theta)
    z_stick = np.zeros(theta.shape[0])

    m = np.dstack([
       z_stick,       -r_hat[:, 0, 2], r_hat[:, 0, 1],
       r_hat[:, 0, 2], z_stick,       -r_hat[:, 0, 0],
      -r_hat[:, 0, 1], r_hat[:, 0, 0], z_stick]
    ).reshape([-1, 3, 3])

    i_cube = np.broadcast_to(
      np.expand_dims(np.eye(3), axis = 0), [theta.shape[0], 3, 3]
    )

    A   = np.transpose(r_hat, axes = [0, 2, 1])
    B   = r_hat
    dot = np.matmul(A, B)

    # Rodrigues' rotation formula
    R = cos * i_cube + (1 - cos) * dot + np.sin(theta) * m

    return R;

  def with_zeros(self, x: np.ndarray) -> np.ndarray:
    '''
    Append a [0, 0, 0, 1] vector to a [3, 4] matrix.

    Parameter:
    ---------
    x: Matrix to be appended.

    Return:
    ------
    Matrix after appending of shape [4,4]
    '''
    return np.vstack((x, np.array([[0.0, 0.0, 0.0, 1.0]])));

  def pack(self, x: np.ndarray) -> np.ndarray:
    '''
    Append zero matrices of shape [4, 3] to vectors of [4, 1] shape in a batched
    manner.

    Parameter:
    ----------
    x: Matrices to be appended of shape [batch_size, 4, 1]

    Return:
    ------
    Matrix of shape [batch_size, 4, 4] after appending.
    '''
    return np.dstack((np.zeros((x.shape[0], 4, 3)), x));
