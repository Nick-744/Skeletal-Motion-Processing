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

import warnings
warnings.filterwarnings('ignore', category = DeprecationWarning)
warnings.filterwarnings('ignore', category = FutureWarning)

from config import *
import numpy as np
import pickle



# ---< Compatibility bs fixing >--- #
np.bool    = np.bool_
np.int     = np.int_
np.float   = np.float64
np.complex = np.complex128
np.object  = object
np.unicode = str
np.str     = str

import inspect
if not hasattr(inspect, 'getargspec'):
  inspect.getargspec = inspect.getfullargspec
#####################################



def prepare_mano_model(
    source_path: str = OFFICIAL_MANO_PATH,
    target_path: str = MANO_MODEL_PATH) -> None:
  ''' Convert the official MANO model into compatible format with this project. '''

  with open(source_path, 'rb') as f:
    data = pickle.load(f, encoding = 'latin1')
  
  params = {
    'pose_pca_basis':   np.array(data['hands_components']),
    'pose_pca_mean':    np.array(data['hands_mean']),
    'J_regressor':      data['J_regressor'].toarray(),
    'skinning_weights': np.array(data['weights']),

    # pose blend shape
    'mesh_pose_basis':  np.array(data['posedirs'  ]),
    'mesh_shape_basis': np.array(data['shapedirs' ]),
    'mesh_template':    np.array(data['v_template']),
    'faces':            np.array(data['f']),
    'parents':          data['kintree_table'][0].tolist()
  }
  params['parents'][0] = None
  
  with open(target_path, 'wb') as f:
    pickle.dump(params, f)
  
  return;



if __name__ == '__main__':
  # Left Hand
  prepare_mano_model()
  print('MANO model (left) prepared and saved to:\n->', MANO_MODEL_PATH)

  # Right Hand
  prepare_mano_model(
    source_path = OFFICIAL_MANO_PATH_RIGHT,
    target_path = MANO_MODEL_PATH_RIGHT
  )
  print('\nMANO model (right) prepared and saved to:\n->', MANO_MODEL_PATH_RIGHT)
