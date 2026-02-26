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

if not os.path.exists(OFFICIAL_MANO_PATH):
    print(f'-> Source model not found at {OFFICIAL_MANO_PATH}')
