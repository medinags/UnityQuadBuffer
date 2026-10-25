# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- User manual in `Documentation~/`.
- Documented public native header `QuadBufferPlugin.h`.
- Native logging to the Unity log (`[QuadBuffer native]`) for device, stereo support, swap chain and presenting.
- Stereo status notifications (`RegisterStereoStatusEvent`), with re-checks of windowed and full-screen stereo support.
- `QuadBufferDiagnostics`: status snapshot, `StatusChanged` event and logging of every state change.
- `QuadBufferConsole`: in-game status and log overlay (F12), automatic in the Editor and development builds.
- Detection of Unity's swap chain model (flip or blit), shown in the log, status (`UnitySwapChainModel`, `FlipModelSwapChain` state) and console.
- Build warning when "Use DXGI flip model swapchain for D3D11" is on.
- `QuadBufferManager`: scene entry point. Uses the camera assigned in the Inspector, otherwise finds
  one at start-up (existing `QuadBufferCamera`, `Camera.main`, first screen camera), otherwise waits
  for `SetCamera(camera)` from a script. Adds the `QuadBufferCamera` and logs each step.
- Play Mode tests for `QuadBufferManager`.
- `QuadBufferManager.Console` (Auto / Always / Never) to get the in-game console in release builds.
  In a release build with Auto, a log line explains how to enable it.

### Changed
- `QuadBufferDiagnostics` no longer starts by itself in every scene: it runs only while a
  `QuadBufferManager` is enabled (`IsRunning`, `Manager`). The console is added by the manager.
- `QuadBufferCamera` without a manager still works, reading the status directly, and logs a warning.
- Package renamed from `com.medinags.quadbuffer` to `com.xra.quadbuffer`; author is Camilo Medina.
- Minimum Unity version is now 6000.6 (was 2022.3, which was never tested).
- `QuadBufferCamera` starts or stops stereo when stereo is turned on or off while running.
- Native state shared between the main and render threads is now guarded by a lock.

## [0.1.0] - 2026-10-18

### Added
- Native D3D11 stereo plugin and C# bindings.
- `QuadBufferCamera` component.
- Build-time check for the Direct3D11 graphics API.
