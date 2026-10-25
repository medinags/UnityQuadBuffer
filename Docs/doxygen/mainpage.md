# QuadBufferPlugin

`QuadBufferPlugin.dll` is a Unity native rendering plugin. It receives Unity's left and
right eye render textures and presents them through a quad-buffered DXGI swap chain.

- **Public C API:** QuadBufferPlugin.h, the functions the Unity package calls through P/Invoke.
- **Presenter:** D3D11StereoPresenter, which owns the stereo swap chain.

Unity calls `UnityPluginLoad` / `UnityPluginUnload` when the DLL is loaded and unloaded, and
the plugin listens for graphics device events to bind to Unity's Direct3D11 device.

See the Architecture section of the main documentation site for the frame flow and threading model.
