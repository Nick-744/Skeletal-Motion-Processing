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

# It must be assured that parent joint appears before child joint!

class MANOArmature:
  # Number of movable joints
  n_joints = 16

  # Indices of extended keypoints
  keypoints_ext = [333, 444, 672, 555, 744]

  n_keypoints = n_joints + len(keypoints_ext)

  labels = [
    'W',              #0
    'I0', 'I1', 'I2', #3
    'M0', 'M1', 'M2', #6
    'L0', 'L1', 'L2', #9
    'R0', 'R1', 'R2', #12
    'T0', 'T1', 'T2', #15
    
    # extended
    'I3', 'M3', 'L3', 'R3', 'T3' #20
  ]

  

  # Kinematic Hierarchy
  # For any joint 'k', snap_parent[k] gives the joint it is attached to.
  snap_parent = [
    0, # 0's parent

    0, # 1's parent
    1, 2, 3,

    0, # 5's parent
    5, 6, 7,

    0, # 9's parent
    9, 10, 11,

    0, # 13's parent
    13, 14, 15,

    0, # 17's parent
    17, 18, 19
  ]

  # The list of joints that the IK solver needs to calculate relative rotations for.
  kinematic_tree = [2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15, 16, 18, 19, 20]

  # Translation dictionary - MediaPipe2MANO
  id_to_rot = {
     2: 13,  3: 14,  4: 15,
     6:  1,  7:  2,  8:  3,
    10:  4, 11:  5, 12:  6,
    14: 10, 15: 11, 16: 12,
    18:  7, 19:  8, 20:  9
  }
