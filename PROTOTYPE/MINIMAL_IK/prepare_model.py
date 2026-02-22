from config import *
import pickle
import numpy as np



# ---< Compatibility solution >--- #
import inspect
import collections

if not hasattr(np, 'bool'   ): np.bool    = np.bool_
if not hasattr(np, 'int'    ): np.int     = np.int64
if not hasattr(np, 'float'  ): np.float   = np.float64
if not hasattr(np, 'complex'): np.complex = np.complex128
if not hasattr(np, 'object' ): np.object  = object
if not hasattr(np, 'str'    ): np.str     = str
if not hasattr(np, 'unicode'): np.unicode = str

if not hasattr(inspect, 'getargspec'):
    def getargspec(func):
        spec = inspect.getfullargspec(func)
        return collections.namedtuple('ArgSpec', 'args varargs keywords defaults')(
            spec.args, spec.varargs, spec.varkw, spec.defaults
        );
    inspect.getargspec = getargspec
####################################



def prepare_mano_model():
  '''
  Convert the official MANO model into compatible format with this project.
  '''
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
    'parents':          data['kintree_table'][0].tolist(),
  }
  params['parents'][0] = None
  with open(MANO_MODEL_PATH, 'wb') as f:
    pickle.dump(params, f)



if __name__ == '__main__':
  prepare_mano_model()
