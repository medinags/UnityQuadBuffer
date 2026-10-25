#pragma once

/**
 * @file QuadBufferPlugin.h
 * @brief Public C API exported by QuadBufferPlugin.dll.
 *
 * These functions are called from C# through P/Invoke
 * (see `QuadBuffer.QuadBufferNative` in the Unity package). All functions use the
 * C calling convention and C linkage so their names are not mangled.
 *
 * Threading: the setters are called from Unity's main thread. Presentation happens on
 * Unity's render thread through the callback returned by QuadBuffer_GetRenderEventFunc().
 */

#include <stdint.h>

#include "IUnityInterface.h"
#include "IUnityGraphics.h"

/**
 * @brief Render event ids accepted by the callback from QuadBuffer_GetRenderEventFunc().
 *
 * Must match `QuadBufferNative.EventPresent` on the C# side.
 */
enum QuadBufferEvent : int
{
    /** Copy the left/right eye textures into the stereo swap chain and present it. */
    kQuadBufferEventPresent = 1,
    /** Only handle pending stereo status notifications. Cheap; issued every frame by the diagnostics. */
    kQuadBufferEventPollStatus = 2,
};

/**
 * @brief Snapshot of the plugin's quad-buffer state, filled by QuadBuffer_GetStatus().
 *
 * Boolean fields are `int32_t` (0 or 1) so the layout is blittable to C#.
 * Must match `QuadBufferStatus` in QuadBufferNative.cs.
 */
struct QuadBufferStatus
{
    /** Stereo frames successfully presented since start-up. */
    int64_t framesPresented;
    /** Unity's graphics API, as a `UnityGfxRenderer` value (2 = Direct3D11). */
    int32_t renderer;
    /** The plugin is bound to Unity's Direct3D11 device. */
    int32_t deviceInitialized;
    /** `IDXGIFactory2::IsWindowedStereoEnabled()`: OS and driver allow windowed stereo. */
    int32_t windowedStereoEnabled;
    /** Stereo display modes reported by `IDXGIOutput1::GetDisplayModeList1(DXGI_ENUM_MODES_STEREO)`, all outputs. */
    int32_t fullscreenStereoModeCount;
    /** Registered with `IDXGIFactory2::RegisterStereoStatusEvent`. */
    int32_t stereoStatusRegistered;
    /** Stereo status notifications received since start-up. */
    int32_t stereoStatusChangeCount;
    /** A target window was set with QuadBuffer_SetWindow(). */
    int32_t hasWindow;
    /** Eye textures were set with QuadBuffer_SetEyeTextures(). */
    int32_t hasEyeTextures;
    /** The eye textures match each other and the swap chain in size and format. */
    int32_t eyeTexturesValid;
    /** The stereo swap chain exists. */
    int32_t swapChainCreated;
    /** The created swap chain reports `Stereo = TRUE`. */
    int32_t swapChainIsStereo;
    /** Most recent failing `HRESULT` (0 if none). */
    int32_t lastErrorHResult;
    /**
     * `DXGI_SWAP_EFFECT` of Unity's own swap chain, or -1 if not known yet.
     * Flip-model values (3, 4) mean Unity's window cannot take a second swap chain: turn off
     * "Use DXGI flip model swapchain for D3D11" in Player Settings.
     */
    int32_t unitySwapEffect;
};

extern "C"
{
    /**
     * @brief Copies the current plugin state into @p status.
     * @param[out] status Receives the snapshot. Ignored if `nullptr`.
     */
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API QuadBuffer_GetStatus(QuadBufferStatus* status);

    /**
     * @brief Removes the oldest queued plugin log message.
     *
     * Every message is also written to Unity's log. The queue lets the C# side show native
     * messages in the in-game console.
     *
     * @param[out] type       `UnityLogType` of the message (0 error, 2 warning, 3 log).
     * @param[out] buffer     Receives the null-terminated UTF-8 message, truncated to fit.
     * @param      bufferSize Size of @p buffer in bytes.
     * @return `true` if a message was returned, `false` if the queue was empty.
     */
    UNITY_INTERFACE_EXPORT bool UNITY_INTERFACE_API QuadBuffer_PopLogMessage(int* type, char* buffer, int bufferSize);

    /**
     * @brief Reports whether quad-buffer stereo output can be used.
     * @return `true` when Unity runs on Direct3D11 and the driver has windowed stereo enabled.
     */
    UNITY_INTERFACE_EXPORT bool UNITY_INTERFACE_API QuadBuffer_IsStereoAvailable();

    /**
     * @brief Sets the window the stereo swap chain presents to.
     *
     * Changing the window releases the current swap chain; a new one is created on the
     * next present.
     *
     * @param hwnd Win32 window handle (`HWND`).
     */
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API QuadBuffer_SetWindow(void* hwnd);

    /**
     * @brief Sets the textures that hold the rendered left and right eye images.
     *
     * The plugin keeps a reference to each texture until they are replaced. Pass `nullptr`
     * for both to release them.
     *
     * @param left  `ID3D11Texture2D*` from `RenderTexture.GetNativeTexturePtr()` for the left eye.
     * @param right `ID3D11Texture2D*` for the right eye. Must match the left eye's size and format.
     */
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API QuadBuffer_SetEyeTextures(void* left, void* right);

    /**
     * @brief Returns the render-thread callback to pass to `GL.IssuePluginEvent`.
     * @return A function that handles ::QuadBufferEvent ids.
     */
    UNITY_INTERFACE_EXPORT UnityRenderingEvent UNITY_INTERFACE_API QuadBuffer_GetRenderEventFunc();
}
