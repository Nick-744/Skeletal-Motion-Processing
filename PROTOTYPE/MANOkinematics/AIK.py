# MIT License
#
# Copyright (c) 2021 Hao Meng
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
import transforms3d
from MANOkinematics import armatures as cfg

# Initialize default twist angles for all 21 joints to 0
angles0 = np.zeros((1, 21))



def to_dict(joints: np.ndarray) -> dict:
    ''' Converts a 3x21 array of joint positions into a dictionary
    mapping joint indices to their corresponding 3D positions. '''

    temp_dict = dict()
    for i in range(21):
        # Shape: (3, 1) - column vector
        temp_dict[i] = joints[:, [i]]

    return temp_dict;



def adaptive_IK(T_: np.ndarray, P_: np.ndarray) -> np.ndarray:
    '''
    Computes pose parameters given template and predictions.
    We think the twist of hand bone could be omitted.

    :param T: template, 21x3 (The default rest pose of the hand)
    :param P: target,   21x3 (The 3D keypoints to be matched)
    :return: pose params.
    '''

    T = T_.copy().astype(np.float64)
    P = P_.copy().astype(np.float64)

    # Transpose the matrices from shape (21, 3) to (3, 21).
    # This turns each joint's coords into a column vector!
    P = P.transpose(1, 0)
    T = T.transpose(1, 0)

    # To dict
    P = to_dict(P)
    T = to_dict(T)

    # Some globals
    R      = {} # Absolute rotation matrix for each joint
    R_pa_k = {} # Relative rotation matrix of a joint with respect to its parent
    q      = {} # Calculated absolute position of each joint, q[0] is root joint position

    # Anchor the root of computed skeleton to the root of the template skeleton!
    q[0] = T[0] # In fact, q[0] = P[0] = T[0].

    # ---< COMPUTE GLOBAL WRIST ROTATION (R0) >--- #

    # Compute R0, here we think R0 is not only a Orthogonal matrix, but also a Rotation matrix.
    # You can refer to paper "Least-Squares Fitting of Two 3-D Point Sets. K. S. Arun; T. S. Huang; S. D. Blostein"
    # It is slightly different from: https://github.com/Jeff-sjtu/HybrIK/blob/main/hybrik/utils/pose_utils.py#L4,
    # in which R0 is regard as orthogonal matrix only. Using their method might further boost accuracy.
    P_0 = np.concatenate([P[ 1] - P[0], P[ 5] - P[0],
                          P[ 9] - P[0], P[13] - P[0],
                          P[17] - P[0]], axis = -1) # Vectors pointing from the wrist to the base of the 5 fingers
    T_0 = np.concatenate([T[ 1] - T[0], T[ 5] - T[0],
                          T[ 9] - T[0], T[13] - T[0],
                          T[17] - T[0]], axis = -1)
    
    # Compute the cross-covariance matrix H
    H = np.matmul(T_0, P_0.T)

    # Perform Singular Value Decomposition (SVD) on H
    (U, S, V_T) = np.linalg.svd(H)
    V           = V_T.T

    # Calculate the optimal rotation matrix R0
    R0 = np.matmul(V, U.T)

    # A valid rotation matrix must have a determinant of +1
    det0 = np.linalg.det(R0)
    if abs(det0 + 1) < 1e-6:
        V_ = V.copy()

        if (abs(S) < 1e-4).sum():
            V_[:, 2] = -V_[:, 2]
            R0       = np.matmul(V_, U.T)
    
    R[0] = R0

    # The bone from 1, 5, 9, 13, 17 to 0 has same rotations!
    R[1]  = R[0].copy()
    R[5]  = R[0].copy()
    R[9]  = R[0].copy()
    R[13] = R[0].copy()
    R[17] = R[0].copy()

    # ---< KINEMATIC CHAIN TRAVERSAL >--- #

    # Compute rotation along kinematics
    for k in cfg.MANOArmature.kinematic_tree:
        # Get the parent and grandparent of current joint k
        pa    = cfg.MANOArmature.snap_parent[k]
        pa_pa = cfg.MANOArmature.snap_parent[pa]

        # Forward Kinematics:
        # Calculate the spatial 3D position of the parent joint
        # by applying the parent's global rotation to the template bone,
        # and adding it to the grandparent's position.
        q[pa] = np.matmul(R[pa], (T[pa] - T[pa_pa])) + q[pa_pa]

        # Calculate the vector from the computed parent to the target joint, 
        # and transform it back into the parent's local coordinate space.
        delta_p_k = np.matmul(np.linalg.inv(R[pa]), P[k] - q[pa])
        delta_p_k = delta_p_k.reshape((3,))

        # Calculate the template bone vector (from parent to current joint)
        delta_t_k = T[k] - T[pa]
        delta_t_k = delta_t_k.reshape((3,))

        # Find the rotation axis required to align the template bone to the target bone
        temp_axis = np.cross(delta_t_k, delta_p_k)
        axis      = temp_axis / (np.linalg.norm(temp_axis, axis = -1) + 1e-8)

        # Calculate the angle between the template and target bones
        temp      = (np.linalg.norm(delta_t_k, axis = 0) + 1e-8) * (np.linalg.norm(delta_p_k, axis = 0) + 1e-8)
        cos_alpha = np.dot(delta_t_k, delta_p_k) / temp
        alpha     = np.arccos(cos_alpha)

        twist = delta_t_k # Twist is defined along the axis of the bone itself

        # Convert the "swing" axis and angle into a 3x3 rotation matrix
        D_sw  = transforms3d.axangles.axangle2mat(axis = axis,  angle = alpha,         is_normalized = False)
        # Convert the "twist" axis and angle into a 3x3 rotation matrix
        D_tw  = transforms3d.axangles.axangle2mat(axis = twist, angle = angles0[0, k], is_normalized = False)

        # Combine swing and twist to get the final
        # relative rotation of joint k from its parent.
        R_pa_k[k] = np.matmul(D_sw, D_tw)

        R[k] = np.matmul(R[pa], R_pa_k[k])

    # ---< FORMATTING THE OUTPUT >--- #

    pose_R       = np.zeros((1, 16, 3, 3))
    pose_R[0, 0] = R[0]
    for key in cfg.MANOArmature.id_to_rot.keys():
        value            = cfg.MANOArmature.id_to_rot[key]
        pose_R[0, value] = R_pa_k[key]

    return pose_R;
