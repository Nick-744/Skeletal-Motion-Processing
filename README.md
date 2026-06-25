# Processing and encoding of skeletal motion models for data normalization and analysis

### Screenshots

![Screenshots Collage](screenshots-collage.png)

---

## Project Overview
A real-time, monocular, CPU-only hand-tracking system that turns a single webcam feed into a canonical MANO hand pose - no GPU, headset, depth camera, or gloves required.

It pairs MediaPipe landmark detection and a 1€ filter with an analytical inverse-kinematics (AIK) solver, then streams the solved joint rotations, gesture, and anchor to Unity over UDP/JSON.

The recovered pose drives four interactive Unity scenes (grappling companion, deformable face, editable city, 3D painting tool), at a 30.68 ms mean software latency on roughly one-third of a commodity laptop CPU.

### Watch the Presentation
[![Thesis Presentation](https://img.shields.io/badge/YouTube-Video-red?style=for-the-badge&logo=youtube)](https://youtu.be/T5l_-FreMbY)

---

## Instructions to Run

Steps to set up the environment and install the required dependencies:

1. **Create the Conda environment:**
```bash
conda create -n skeletal_game_env python=3.12 -y
```

2. **Activate the environment:**
```bash
conda activate skeletal_game_env
```

3. **Install the required packages:**
```bash
pip install opencv-python==4.13.0.92 mediapipe==0.10.35 numpy==2.4.2 matplotlib==3.10.8 transforms3d==0.4.2 open3d==0.19.0
```
