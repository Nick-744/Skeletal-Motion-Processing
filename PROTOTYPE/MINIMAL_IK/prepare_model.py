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



def prepare_mano_model():
  ''' Convert the official MANO model into compatible format with this project. '''

  with open(OFFICIAL_MANO_PATH, 'rb') as f:
    data = pickle.load(f, encoding = 'latin1')
  
  params = {
    'pose_pca_basis':   np.array(data['hands_components']),
    'pose_pca_mean':    np.array(data['hands_mean']),
    'J_regressor':      data['J_regressor'].toarray(),
    'skinning_weights': np.array(data['weights']),

    # pose blend shape
    'mesh_pose_basis':  np.array(data['posedirs']),
    'mesh_shape_basis': np.array(data['shapedirs']),
    'mesh_template':    np.array(data['v_template']),
    'faces':            np.array(data['f']),
    'parents':          data['kintree_table'][0].tolist()
  }
  params['parents'][0] = None
  
  with open(MANO_MODEL_PATH, 'wb') as f:
    pickle.dump(params, f)
  
  return;



if __name__ == '__main__':
  prepare_mano_model()
  print('MANO model prepared and saved to:\n->', MANO_MODEL_PATH)
