#pragma once

#include <d3d11.h>
#include <dxgi1_2.h>

#include <mutex>

#include "QuadBufferPlugin.h"
#include "IUnityGraphicsD3D11.h"

/**
 * @brief Presents Unity's eye textures through a quad-buffered DXGI swap chain.
 *
 * Owns a stereo (`DXGI_SWAP_CHAIN_DESC1::Stereo = TRUE`) swap chain and, on each
 * Present(), copies the left/right eye textures into back-buffer array slices 0 and 1.
 *
 * Follows Microsoft's stereo status guidance: checks `IsWindowedStereoEnabled` and the
 * outputs' stereo display modes at start-up, registers for stereo status notifications,
 * and checks again whenever a notification arrives.
 *
 * SetWindow(), SetEyeTextures() and GetStatus() are called from Unity's main thread;
 * Initialize(), Shutdown() and Present() from the render thread. A mutex guards the state
 * they share.
 */
class D3D11StereoPresenter
{
public:
    /**
     * @brief Binds to Unity's D3D11 device, checks stereo support and registers for status changes.
     * @param unityGraphics Unity's D3D11 interface, for its device and its own swap chain.
     * @return `true` if the device could be used. Stereo may still be disabled; see GetStatus().
     */
    bool Initialize(IUnityGraphicsD3D11* unityGraphics);

    /** @brief Unregisters notifications and releases the swap chain, textures and device context. */
    void Shutdown();

    /**
     * @brief Sets the target window and drops the current swap chain.
     * @param hwnd Window to present to. A new swap chain is created lazily on the next Present().
     */
    void SetWindow(HWND hwnd);

    /**
     * @brief Replaces the eye textures, taking a COM reference to each.
     * @param left  Left eye texture, or `nullptr`.
     * @param right Right eye texture, or `nullptr`.
     */
    void SetEyeTextures(ID3D11Texture2D* left, ID3D11Texture2D* right);

    /**
     * @brief Handles pending stereo status changes, then copies both eyes into the stereo
     *        back buffer and presents with vsync.
     */
    void Present();

    /** @brief Handles pending stereo status notifications without presenting. */
    void PollStatus();

    /**
     * @brief Fills the presenter's part of the status (everything except `renderer`).
     * @param[out] status Snapshot to fill.
     */
    void GetStatus(QuadBufferStatus& status);

private:
    void QueryStereoSupport();
    void QueryUnitySwapChain();
    void HandleStereoStatusChange();
    bool CreateSwapChain(UINT width, UINT height);
    bool ValidateEyeTextures();
    void ReleaseSwapChain();
    void SetError(HRESULT hr);

    std::mutex m_mutex;

    IUnityGraphicsD3D11* m_unityGraphics = nullptr;
    ID3D11Device*        m_device = nullptr;
    ID3D11DeviceContext* m_context = nullptr;
    IDXGIAdapter*        m_adapter = nullptr;
    IDXGIFactory2*       m_factory = nullptr;
    IDXGISwapChain1*     m_swapChain = nullptr;
    HWND                 m_hwnd = nullptr;

    ID3D11Texture2D* m_left = nullptr;
    ID3D11Texture2D* m_right = nullptr;

    HANDLE m_stereoStatusEvent = nullptr;
    DWORD  m_stereoStatusCookie = 0;

    bool    m_windowedStereoEnabled = false;
    bool    m_swapChainIsStereo = false;
    bool    m_eyeTexturesChecked = false;
    bool    m_eyeTexturesValid = false;
    bool    m_swapChainFailed = false;
    int     m_fullscreenStereoModeCount = 0;
    int     m_stereoStatusChangeCount = 0;
    int64_t m_framesPresented = 0;
    HRESULT m_lastError = S_OK;
    HRESULT m_lastPresentResult = S_OK;
    int     m_unitySwapEffect = -1; // DXGI_SWAP_EFFECT of Unity's swap chain; -1 until known.
};
