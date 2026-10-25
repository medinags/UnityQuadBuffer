# 0001. Present stereo through a native DXGI swap chain

- **Status:** Accepted
- **Date:** 2026-10-18

## Context

Quad-buffered stereo needs a swap chain with separate left and right back buffers. Unity
removed its built-in non-XR stereo display support, and its own swap chain is always mono.
There is no managed API for creating a stereo swap chain.

## Decision

Write a Unity native rendering plugin that:

- binds to Unity's Direct3D11 device through `IUnityGraphicsD3D11`,
- creates its own swap chain with `DXGI_SWAP_CHAIN_DESC1::Stereo = TRUE`,
- copies Unity-rendered eye textures into back-buffer slices 0 and 1 on the render thread.

Direct3D11 is the only supported graphics API for now.

## Consequences

- Works on any GPU and driver that reports `IsWindowedStereoEnabled()`.
- Users must turn off Auto Graphics API and choose Direct3D11.
- The plugin needs a window without a flip-model swap chain (DXGI allows one per window).
  See [ADR 0004](0004-share-unity-window-blit-model.md) for how this is solved.
- Each eye is copied once per frame, a small extra GPU cost.

## Alternatives considered

- **OpenGL quad-buffered pixel format (`PFD_STEREO`).** OpenGL Core is still available on
  Windows (checked in 6000.6.2f1), but Unity doesn't expose the pixel format, so it needs a
  custom player launcher that hooks pixel format selection. Kept as the fallback
  ([ADR 0004](0004-share-unity-window-blit-model.md)).
- **Direct3D12.** Supports stereo swap chains too, but makes texture sharing and
  synchronization with Unity more complex. Possible later addition.
- **Vendor stereo APIs (e.g. NVAPI 3D Vision).** Tied to one vendor and largely discontinued.
