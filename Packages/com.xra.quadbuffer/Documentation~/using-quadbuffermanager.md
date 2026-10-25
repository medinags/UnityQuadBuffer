# Using QuadBufferManager

`QuadBufferManager` is the entry point for quad-buffer stereo in a scene. Add one per scene:

1. Create an empty GameObject, for example **QuadBuffer Manager**.
2. Add **QuadBuffer > Quad Buffer Manager**.
3. Optionally drag the stereo camera into **Target Camera**.

The manager:

- puts a [QuadBufferCamera](using-quadbuffercamera.md) on the stereo camera,
- runs the diagnostics: status, `[QuadBuffer]` logs and stereo on/off detection
  (see [Status and logs](diagnostics.md)),
- adds the in-game console according to its **Console** setting: **Auto** (Editor, development
  builds, `-quadbuffer-console`), **Always** (also release builds) or **Never**.

Without a manager, none of the diagnostics run.

## How the camera is chosen

| Order | Source | Log message |
|---|---|---|
| 1 | **Target Camera** assigned in the Inspector | `using the camera assigned in the Inspector, '<name>'` |
| 2 | At start-up, a camera that already has a `QuadBufferCamera` | `no camera assigned; using '<name>' (it already has a QuadBufferCamera)` |
| 3 | At start-up, `Camera.main` (tag **MainCamera**) | `... (Camera.main)` |
| 4 | At start-up, the first enabled camera that renders to the screen | `... (first enabled camera rendering to the screen)` |
| 5 | None found | Warning: `no camera assigned and none found in the scene ...` |

If the camera already has a `QuadBufferCamera`, the manager uses it. Otherwise it adds one and
logs `added QuadBufferCamera to '<name>'`.

## Setting the camera from a script

When the camera is created later (for example spawned with the player rig), pass it to the
manager:

```csharp
using QuadBuffer;
using UnityEngine;

public class PlayerRig : MonoBehaviour
{
    [SerializeField] QuadBufferManager quadBuffer;
    [SerializeField] Camera playerCamera;

    void Start()
    {
        quadBuffer.SetCamera(playerCamera);
    }
}
```

`SetCamera` can be called at any time, including before the manager's own `Start`:

- **New camera:** it gets a `QuadBufferCamera`, and stereo moves to it.
- **Previous camera:** stops at once. If the manager added its `QuadBufferCamera`, it is removed;
  if the camera already had one, it is disabled instead.
- **Same camera again:** nothing changes (logged).
- **`null`:** rejected with an error; the current camera is kept.

## Scripting

| Member | Description |
|---|---|
| `TargetCamera` | The camera rendered in stereo, or `null`. |
| `StereoCamera` | The `QuadBufferCamera` on that camera, or `null`. |
| `SetCamera(Camera)` | Sets the stereo camera, as described above. |
| `Console` | `ConsoleMode.Auto`, `Always` or `Never`. Read when the manager is enabled. |
| `QuadBufferDiagnostics.Manager` | The manager driving the diagnostics, or `null`. |
| `QuadBufferDiagnostics.IsRunning` | `true` while a manager is enabled. |

## More than one manager

Only the first enabled manager drives the diagnostics; the others log a warning. Disabling or
destroying the active manager stops the diagnostics (logged) until another manager is enabled.

## Without a manager

A `QuadBufferCamera` added by hand still renders stereo, reading the status directly from the
plugin. It logs a warning that diagnostics, the console and stereo on/off detection while
waiting are off.
