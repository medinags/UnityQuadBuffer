# Basic Stereo Sample

1. Create a scene with a camera, and add a GameObject with a `QuadBufferManager` (it finds the main camera
   and adds the `QuadBufferCamera`).
2. Set **Player Settings > Other Settings > Graphics APIs for Windows** to `Direct3D11` only.
3. Enable stereo 3D in the GPU driver (e.g. NVIDIA Quadro "Stereo – Enable").
4. Build a Windows x64 standalone player and run it.
