import os

# Get the absolute path (config.py)
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# Define the folder for processed models
OUTPUT_DIR = os.path.abspath(os.path.join(BASE_DIR, '..', 'ASSETS', 'processed_models'))

# Automatically create the directory if it's missing...
if not os.path.exists(OUTPUT_DIR):
    os.makedirs(OUTPUT_DIR)
    print(f'-> Created missing directory: {OUTPUT_DIR}')

# Define the absolute file paths
MANO_MODEL_PATH    = os.path.join(OUTPUT_DIR, 'mano_left.pkl')
OFFICIAL_MANO_PATH = os.path.abspath(os.path.join(BASE_DIR, '..', 'ASSETS', 'mano', 'MANO_LEFT.pkl'))

# MANO_MODEL_PATH    = os.path.join(OUTPUT_DIR, 'mano_right.pkl')
# OFFICIAL_MANO_PATH = os.path.abspath(os.path.join(BASE_DIR, '..', 'ASSETS', 'mano', 'MANO_RIGHT.pkl'))

if not os.path.exists(OFFICIAL_MANO_PATH):
    print(f'-> Source model not found at {OFFICIAL_MANO_PATH}')



### Constants ###

DEPTH_RANGE = 3.0
DEPTH_MIN   = -1.5

SNAP_PARENT = [
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

kinematic_tree = [2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15, 16, 18, 19, 20]

ID2ROT = {
         2: 13,  3: 14,  4: 15,
         6:  1,  7:  2,  8:  3,
        10:  4, 11:  5, 12:  6,
        14: 10, 15: 11, 16: 12,
        18:  7, 19:  8, 20:  9
    }
