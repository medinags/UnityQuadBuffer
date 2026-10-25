// Unity native rendering plugin entry points for quad-buffer stereo output.
// The public API is documented in include/QuadBufferPlugin.h.

// D3D headers (via the presenter) must come before IUnityGraphicsD3D11.h.
#include "D3D11StereoPresenter.h"

#include "QuadBufferPlugin.h"
#include "IUnityGraphicsD3D11.h"
#include "Log.h"

namespace
{
    IUnityInterfaces*    s_unityInterfaces = nullptr;
    IUnityGraphics*      s_graphics = nullptr;
    D3D11StereoPresenter s_presenter;
    UnityGfxRenderer     s_renderer = kUnityGfxRendererNull;
    bool                 s_initialized = false;

    const char* RendererName(UnityGfxRenderer renderer)
    {
        switch (renderer)
        {
        case kUnityGfxRendererD3D11:      return "Direct3D11";
        case kUnityGfxRendererD3D12:      return "Direct3D12";
        case kUnityGfxRendererVulkan:     return "Vulkan";
        case kUnityGfxRendererOpenGLCore: return "OpenGLCore";
        case kUnityGfxRendererNull:       return "Null (batch mode / no graphics)";
        default:                          return "other";
        }
    }

    void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
    {
        switch (eventType)
        {
        case kUnityGfxDeviceEventInitialize:
            s_renderer = s_graphics->GetRenderer();
            if (s_renderer == kUnityGfxRendererD3D11)
            {
                QuadBufferLog::Info("Graphics device initialized: %s.", RendererName(s_renderer));
                s_initialized = s_presenter.Initialize(s_unityInterfaces->Get<IUnityGraphicsD3D11>());
            }
            else
            {
                QuadBufferLog::Warning("Graphics API is %s (%d). Quad buffering needs Direct3D11; "
                                       "it stays disabled.", RendererName(s_renderer), s_renderer);
            }
            break;
        case kUnityGfxDeviceEventShutdown:
            if (s_initialized)
                QuadBufferLog::Info("Graphics device shutting down.");
            s_presenter.Shutdown();
            s_initialized = false;
            break;
        default:
            break;
        }
    }

    void UNITY_INTERFACE_API OnRenderEvent(int eventId)
    {
        if (!s_initialized)
            return;
        if (eventId == kQuadBufferEventPresent)
            s_presenter.Present();
        else if (eventId == kQuadBufferEventPollStatus)
            s_presenter.PollStatus();
    }
}

// Called by Unity when the plugin DLL is loaded.
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    s_unityInterfaces = unityInterfaces;
    QuadBufferLog::SetUnityLog(unityInterfaces->Get<IUnityLog>());
    QuadBufferLog::Info("Plugin loaded.");

    s_graphics = unityInterfaces->Get<IUnityGraphics>();
    s_graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
    // The device may already exist when the plugin loads.
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

// Called by Unity before the plugin DLL is unloaded.
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    s_graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    OnGraphicsDeviceEvent(kUnityGfxDeviceEventShutdown);
    QuadBufferLog::Info("Plugin unloaded.");
    QuadBufferLog::SetUnityLog(nullptr);
}

UNITY_INTERFACE_EXPORT bool UNITY_INTERFACE_API QuadBuffer_IsStereoAvailable()
{
    if (!s_initialized)
        return false;
    QuadBufferStatus status = {};
    s_presenter.GetStatus(status);
    return status.windowedStereoEnabled != 0;
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API QuadBuffer_SetWindow(void* hwnd)
{
    s_presenter.SetWindow(static_cast<HWND>(hwnd));
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API QuadBuffer_SetEyeTextures(void* left, void* right)
{
    s_presenter.SetEyeTextures(static_cast<ID3D11Texture2D*>(left), static_cast<ID3D11Texture2D*>(right));
}

UNITY_INTERFACE_EXPORT UnityRenderingEvent UNITY_INTERFACE_API QuadBuffer_GetRenderEventFunc()
{
    return OnRenderEvent;
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API QuadBuffer_GetStatus(QuadBufferStatus* status)
{
    if (!status)
        return;
    *status = {};
    s_presenter.GetStatus(*status);
    status->renderer = s_renderer;
}

UNITY_INTERFACE_EXPORT bool UNITY_INTERFACE_API QuadBuffer_PopLogMessage(int* type, char* buffer, int bufferSize)
{
    return QuadBufferLog::Pop(type, buffer, bufferSize);
}
