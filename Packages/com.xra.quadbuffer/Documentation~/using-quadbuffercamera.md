# Using QuadBufferCamera

`QuadBufferCamera` is added to an existing camera. When stereo is available it:

1. Creates two child cameras, **Left Eye** and **Right Eye**, copied from the parent camera's
   settings and offset sideways by half the eye separation each.
2. Renders each eye into its own `RenderTexture` the size of the screen.
3. Passes both textures and the player window to the native plugin.
4. At the end of every frame, issues a plugin event on the render thread that presents both
   eyes through the stereo swap chain.

The parent camera keeps rendering normally, so the mono image remains on screen if stereo
presentation fails.

## Properties

| Property | Description |
|---|---|
| Eye Separation | Distance between the left and right eye cameras, in world units. Can be changed at run time. |

## Scripting

| Member | Description |
|---|---|
| `IsStereoAvailable` | `true` once the plugin has confirmed stereo output can be used. Always `false` in the Editor. |

Disabling the component destroys the eye cameras and render textures and releases them in
the plugin. Enabling it again recreates them.
