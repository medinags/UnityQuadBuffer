# 0004. Share Unity's window by turning off the flip model swap chain

- **Status:** Accepted (not yet verified on stereo hardware)
- **Date:** 2026-10-24

## Context

The stereo swap chain must use the DXGI flip model, and DXGI allows only one flip-model swap
chain per window. Unity's own Direct3D11 swap chain uses the flip model by default
(**Use DXGI flip model swapchain for D3D11**), so creating the stereo swap chain on Unity's
window fails ([ADR 0001](0001-native-dxgi-stereo-swap-chain.md)).

The [Stereoscopic 3D system for Unity](https://github.com/Vital-Volkov/Stereoscopic-3D-system-for-Unity-2019-)
project presents its Direct3D11 stereo output into Unity's own windows and turns this setting
off, which lets its swap chain share the window.

## Decision

- Require **Use DXGI flip model swapchain for D3D11** to be off for quad buffering.
- The package **never changes the setting itself**. The build validator warns when it's on;
  the test project's **QuadBuffer > Configure Project** turns it off explicitly.
- The plugin reads Unity's swap chain (`IUnityGraphicsD3D11::GetSwapChain`) and reports its
  model in the log and status (`FlipModelSwapChain` state in players).

## Consequences

- No child window: no input forwarding or window tracking.
- The blit model can add latency and is less efficient under the Windows compositor.
- Unity and the plugin both present each frame, so two vsync waits per frame remain possible.
- A project setting is now part of the requirements, so diagnostics must make it visible.

## Alternatives considered

- **Child window covering Unity's client area.** Works with the flip model, but needs window
  creation on the main thread, input pass-through and resize tracking.
- **OpenGL quad-buffered pixel format** via a custom player launcher that hooks pixel format
  selection. Unity presents stereo itself, so no second swap chain at all; needs OpenGLCore.
- **Changing the setting automatically on Editor load.** Rejected: silently changing project
  settings surprises users.
