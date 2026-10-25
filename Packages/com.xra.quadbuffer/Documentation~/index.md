# Quad Buffer Stereo

Quad Buffer Stereo adds quad-buffered (active, frame-sequential) stereo output to Unity
Windows standalone players. It is meant for professional stereo displays and projectors
driven by shutter glasses, where the GPU driver alternates left and right images.

Unity renders each eye into its own render texture. A native Direct3D11 plugin copies the
two images into a DXGI stereo swap chain and presents it.

## Requirements

| Requirement | Value |
|---|---|
| Unity | 6000.6 or newer (tested with 6000.6.2f1) |
| Platform | Windows x64 standalone player |
| Graphics API | Direct3D11 only |
| GPU / driver | Windowed stereo enabled (e.g. NVIDIA professional GPUs with "Stereo – Enable") |

The Editor never shows stereo: the Game view renders the mono camera only.

## Contents

- [Getting started](getting-started.md): install the package and set up a scene.
- [Using QuadBufferManager](using-quadbuffermanager.md): the scene entry point, camera selection and `SetCamera`.
- [Using QuadBufferCamera](using-quadbuffercamera.md): component settings and behavior.
- [Status and logs](diagnostics.md): check whether quad buffering is supported and working.
- [Troubleshooting](troubleshooting.md): what to check when stereo does not appear.

## Known limitations

- The stereo swap chain shares Unity's window, which only works with the DXGI flip model
  swap chain turned off in Player Settings. This has not been tested on stereo hardware yet.
- Eye textures are not recreated when the window is resized.
