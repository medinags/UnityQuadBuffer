# Status and logs

The package reports whether quad buffering is supported and working in three places:

- **Unity's log**: the Editor Console, or `Player.log` in a player
  (`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Player.log`).
- **The in-game console**: an overlay with the live status and the log.
- **Scripting**: `QuadBufferDiagnostics.Status`.

All of this runs only while a [QuadBufferManager](using-quadbuffermanager.md) is enabled in the
scene. Without one, the C# side checks and logs nothing, and the console shows "no QuadBufferManager";
the native plugin still writes its `[QuadBuffer native]` lines to Unity's log.

## Log messages

| Prefix | Source |
|---|---|
| `[QuadBuffer]` | The package's C# code: environment, status changes, camera start/stop. |
| `[QuadBuffer native]` | The native plugin: graphics device, stereo support, swap chain, presenting. |

At start-up the log shows the environment (Unity version, OS, GPU, driver, graphics API,
color space, screen mode and log file path) and the first status. After that, a message is
logged each time the status changes, so a healthy run looks like this:

```
[QuadBuffer native] Plugin loaded.
[QuadBuffer native] Graphics device initialized: Direct3D11.
[QuadBuffer native] GPU: NVIDIA RTX A4000 (vendor 0x10DE)
[QuadBuffer native] Stereo support: windowed stereo enabled = yes, full-screen stereo modes = 12.
[QuadBuffer native] Registered for stereo status notifications.
[QuadBuffer native] Unity's swap chain uses DISCARD (blit model), so the stereo swap chain can share Unity's window.
[QuadBuffer] Diagnostics started by QuadBufferManager 'QuadBuffer Manager'.
[QuadBuffer] Status: Idle. Stereo is supported, but no camera is sending eye images. ...
[QuadBuffer] Manager 'QuadBuffer Manager': using the camera assigned in the Inspector, 'Main Camera'.
[QuadBuffer] Manager 'QuadBuffer Manager': added QuadBufferCamera to 'Main Camera'.
[QuadBuffer] 'Main Camera': stereo rendering started. Eye textures 1920x1080, eye separation 0.064.
[QuadBuffer native] Created stereo swap chain 1920x1080.
[QuadBuffer native] First stereo frame presented. Quad buffering is active.
[QuadBuffer] Status: Presenting. Quad buffering is active.
```

The plugin also reads Unity's own swap chain and logs its model. With the flip model on, the
status is `FlipModelSwapChain`, and creating the stereo swap chain fails with `E_ACCESSDENIED` or
`DXGI_ERROR_INVALID_CALL`. In the Editor, the model shown is the Editor's own swap chain, which
doesn't follow Player Settings, so check it in a player build.

Repeated per-frame problems, such as a failing `Present`, are logged once when they start
and again when they clear, not every frame.

## Stereo on/off while running

Following [Microsoft's guidance](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/stereo-rendering-stereo-status-notifying),
the plugin checks stereo support at start-up with `IDXGIFactory2::IsWindowedStereoEnabled`
(windowed) and `IDXGIOutput1::GetDisplayModeList1` with `DXGI_ENUM_MODES_STEREO`
(full-screen modes), then registers with `IDXGIFactory2::RegisterStereoStatusEvent`.

When the user turns stereo on or off in the driver or Windows display settings, the plugin
logs the notification and checks again, because the notification itself doesn't say what
changed. `QuadBufferCamera` then starts or stops stereo rendering on its own.

## Status values

| State | Meaning |
|---|---|
| `UnsupportedPlatform` | Not running on Windows. |
| `PluginMissing` | `QuadBufferPlugin.dll` could not be loaded. |
| `WrongGraphicsApi` | Unity is not using Direct3D11. |
| `DeviceNotReady` | The plugin has not bound to the graphics device yet. |
| `StereoDisabled` | The OS or GPU driver has stereo turned off. |
| `FlipModelSwapChain` | Unity's swap chain uses the DXGI flip model, which blocks the stereo swap chain. Turn off **Use DXGI flip model swapchain for D3D11** and rebuild. Players only. |
| `Idle` | Stereo is supported, but no camera is sending eye images (the manager found no camera, or this is the Editor, where `QuadBufferCamera` doesn't present). |
| `NoWindow` | The player window handle could not be found. |
| `SwapChainFailed` | Creating the stereo swap chain failed; the log has the `HRESULT` and a hint. |
| `SwapChainNotStereo` | The driver created a mono swap chain. |
| `InvalidEyeTextures` | Eye textures don't match the swap chain; the log says how. |
| `Starting` | Set up; waiting for the first stereo frame. |
| `Presenting` | Quad buffering is working. |
| `Stalled` | No new stereo frame for over a second (window hidden or minimized, or `Present` failing). |

## In-game console

Press **F12** to show or hide it. It shows:

- the status, with every check (device, windowed stereo, full-screen stereo modes,
  notifications, Unity swap chain model, window, eye textures, stereo swap chain, frames
  presented, last error),
- the environment,
- the log, with filters for type and for QuadBuffer messages only, optional stack traces,
  **Copy** (status, environment and log to the clipboard) and **Clear**.

While hidden, a small badge in the top-right corner shows the current status.

When a QuadBufferManager starts, it adds the console to its GameObject, depending on the
manager's **Console** setting:

| Console | Console is added |
|---|---|
| **Auto** (default) | In the Editor (Play mode), in development builds, and in any build started with `-quadbuffer-console` (for example `QuadBufferTest.exe -quadbuffer-console`). |
| **Always** | In every build, including release builds. Use this for stereo test machines. |
| **Never** | Never. |

In a release build with **Auto**, F12 does nothing; the log then says
`In-game console not added (release build)` and how to enable it. The diagnostics themselves
still run and write to `Player.log`.

You can also add **QuadBuffer > Quad Buffer Console** to a GameObject yourself. Its properties:

| Property | Description |
|---|---|
| Toggle Key | Key that shows or hides the console. Default F12. |
| Show On Start | Show the console when the player starts. |
| Show Status Badge | Show the status badge while the console is hidden. |
| Max Entries | Oldest messages are dropped beyond this count. |

## Scripting

```csharp
using QuadBuffer;
using UnityEngine;

public class StereoIndicator : MonoBehaviour
{
    void OnEnable()  => QuadBufferDiagnostics.StatusChanged += OnStatus;
    void OnDisable() => QuadBufferDiagnostics.StatusChanged -= OnStatus;

    void OnStatus(QuadBufferStatus status)
    {
        Debug.Log($"Stereo working: {status.IsWorking} ({status.State}: {status.Explanation})");
    }
}
```

`QuadBufferDiagnostics.Status` always holds the latest snapshot, refreshed every frame.
