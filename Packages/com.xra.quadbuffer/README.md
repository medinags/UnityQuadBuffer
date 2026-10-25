# Quad Buffer Stereo

Quad-buffered (active stereo) output for Unity Windows standalone players.

`QuadBufferCamera` renders left/right eyes into render textures; the native plugin
(`Runtime/Plugins/x86_64/QuadBufferPlugin.dll`, built from `NativePlugin/` in the repo)
copies them into a DXGI stereo swap chain and presents it.

## Requirements
- Unity 6000.6 or newer (tested with 6000.6.2f1)
- Windows x64 standalone player
- Graphics API: Direct3D11 only
- Player Settings: "Use DXGI flip model swapchain for D3D11" off
- GPU/driver with windowed stereo enabled (e.g. NVIDIA Quadro / RTX professional)

## Install
Add to `Packages/manifest.json`:

```json
"com.xra.quadbuffer": "file:<path-to-repo>/Packages/com.xra.quadbuffer"
```
