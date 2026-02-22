import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

OUTPUT_DIR = os.path.abspath(os.path.join(BASE_DIR, '..', 'ASSETS', 'my_MANO_models'))
if not os.path.exists(OUTPUT_DIR):
    os.makedirs(OUTPUT_DIR)
    print(f'-> Created missing directory: {OUTPUT_DIR}')



MANO_MODEL_PATH    = os.path.join(OUTPUT_DIR, 'mano_left.pkl')
OFFICIAL_MANO_PATH = os.path.abspath(os.path.join(BASE_DIR, '..', 'ASSETS', 'mano_v1_2', 'models', 'MANO_LEFT.pkl'))

if not os.path.exists(OFFICIAL_MANO_PATH): print(f'-> Source model not found at {OFFICIAL_MANO_PATH}!')
