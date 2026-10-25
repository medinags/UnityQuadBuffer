# Getting started

## Install the package

Add the package to your project's `Packages/manifest.json`, pointing at your checkout:

```json
{
  "dependencies": {
    "com.xra.quadbuffer": "file:<path-to-repo>/Packages/com.xra.quadbuffer"
  }
}
```

Or use **Window > Package Manager > + > Install package from disk** and select the
package's `package.json`.

## Configure the project

1. Open **Edit > Project Settings > Player > Other Settings**.
2. Under **Windows**, clear **Auto Graphics API for Windows**.
3. Make **Direct3D11** the only entry in the **Graphics APIs for Windows** list.
4. Under **Resolution and Presentation**, clear **Use DXGI flip model swapchain for D3D11**.
   DXGI allows only one flip-model swap chain per window, so with this on, the stereo swap
   chain can't share Unity's window.

The package logs a warning during the build if a Windows build is not set to Direct3D11 only, or
if the flip model swap chain is on. It never changes these settings for you.

## Set up a scene

1. Create an empty GameObject and add **QuadBuffer > Quad Buffer Manager**.
2. Drag the camera you want to render in stereo into **Target Camera**. If you leave it empty,
   the manager uses the main camera, or a script can pass one with `SetCamera`
   (see [Using QuadBufferManager](using-quadbuffermanager.md)).
3. Enter Play mode once: the manager adds a **Quad Buffer Camera** to that camera. To set
   **Eye Separation** (default 0.064, an average human interpupillary distance in meters) in the
   Inspector, add the **Quad Buffer Camera** to the camera yourself; the manager then uses it.

## Build and run

1. Enable stereo 3D in the GPU driver control panel.
2. Build a **Windows, x86_64** player.
3. Run the player on the stereo display.

If stereo is not available at run time, the player logs a warning and keeps rendering in mono.
