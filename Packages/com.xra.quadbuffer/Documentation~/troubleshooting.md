# Troubleshooting

Start with the status: press **F12** in a development build (or run the player with
`-quadbuffer-console`), or search the log for `[QuadBuffer]`. Each status value is explained
in [Status and logs](diagnostics.md).

## "Stereo output only works in a Windows standalone player."

Expected in the Editor and on other platforms. Build a Windows x86_64 player.

## Status `WrongGraphicsApi` or `StereoDisabled`

The plugin either did not get a Direct3D11 device or the driver does not report windowed stereo.

- Check the player log (`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Player.log`)
  for the graphics API in use. It must be Direct3D 11.
- Make Direct3D11 the only entry in **Graphics APIs for Windows**.
- Enable stereo 3D in the GPU driver control panel and restart the player.
- Confirm the display and cable support stereo at the selected refresh rate.

## Status `FlipModelSwapChain`, or `SwapChainFailed` with `E_ACCESSDENIED`

Unity's window already has a flip-model swap chain, and DXGI allows only one per window.

- Turn off **Player Settings > Resolution and Presentation > Use DXGI flip model swapchain for D3D11**.
- Rebuild the player. The setting has no effect in the Editor.
- The log line `Unity's swap chain uses ...` confirms which model the player used.

## `DllNotFoundException: QuadBufferPlugin`

The native plugin is missing or was not included in the build.

- Check that `Runtime/Plugins/x86_64/QuadBufferPlugin.dll` exists in the package.
- In its import settings, make sure **Standalone > Windows x86_64** is enabled.

## The image is mono

The parent camera still renders to the screen. If no stereo image appears, the stereo swap
chain was not created. The console or log shows why; check the status first.
