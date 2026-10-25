#include "D3D11StereoPresenter.h"

#include <vector>

#include "Log.h"

template <typename T>
static void SafeRelease(T*& p)
{
    if (p) { p->Release(); p = nullptr; }
}

static const char* YesNo(bool value) { return value ? "yes" : "no"; }

// Eye textures are copied raw into an R8G8B8A8_UNORM back buffer, so they must be in the
// same typeless format family.
static bool IsBackBufferCompatible(DXGI_FORMAT format)
{
    return format == DXGI_FORMAT_R8G8B8A8_TYPELESS ||
           format == DXGI_FORMAT_R8G8B8A8_UNORM ||
           format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
}

static const char* DescribeHResult(HRESULT hr)
{
    switch (hr)
    {
    case E_ACCESSDENIED:              return "E_ACCESSDENIED: the window probably already has a swap chain (Unity's own)";
    case E_INVALIDARG:                return "E_INVALIDARG";
    case E_OUTOFMEMORY:               return "E_OUTOFMEMORY";
    case DXGI_ERROR_INVALID_CALL:     return "DXGI_ERROR_INVALID_CALL: stereo not supported for this window/format, or the window already has a swap chain";
    case DXGI_ERROR_UNSUPPORTED:      return "DXGI_ERROR_UNSUPPORTED";
    case DXGI_ERROR_DEVICE_REMOVED:   return "DXGI_ERROR_DEVICE_REMOVED";
    case DXGI_ERROR_DEVICE_RESET:     return "DXGI_ERROR_DEVICE_RESET";
    case DXGI_STATUS_OCCLUDED:        return "DXGI_STATUS_OCCLUDED: window is hidden or minimized";
    default:                          return "see winerror.h";
    }
}

static bool IsFlipModel(int swapEffect)
{
    return swapEffect == DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL || swapEffect == DXGI_SWAP_EFFECT_FLIP_DISCARD;
}

static const char* SwapEffectName(int swapEffect)
{
    switch (swapEffect)
    {
    case DXGI_SWAP_EFFECT_DISCARD:         return "DISCARD (blit model)";
    case DXGI_SWAP_EFFECT_SEQUENTIAL:      return "SEQUENTIAL (blit model)";
    case DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL: return "FLIP_SEQUENTIAL (flip model)";
    case DXGI_SWAP_EFFECT_FLIP_DISCARD:    return "FLIP_DISCARD (flip model)";
    default:                               return "unknown";
    }
}

bool D3D11StereoPresenter::Initialize(IUnityGraphicsD3D11* unityGraphics)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_device)
        return true; // Already bound (UnityPluginLoad and the device event can both initialize).

    m_unityGraphics = unityGraphics;
    m_device = unityGraphics->GetDevice();
    m_device->GetImmediateContext(&m_context);

    IDXGIDevice* dxgiDevice = nullptr;
    HRESULT hr = m_device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));
    if (SUCCEEDED(hr)) hr = dxgiDevice->GetAdapter(&m_adapter);
    if (SUCCEEDED(hr)) hr = m_adapter->GetParent(__uuidof(IDXGIFactory2), reinterpret_cast<void**>(&m_factory));
    SafeRelease(dxgiDevice);

    if (FAILED(hr))
    {
        SetError(hr);
        QuadBufferLog::Error("Could not get the DXGI 1.2 factory from Unity's device (hr=0x%08X). "
                             "Stereo needs Windows 8 or newer.", static_cast<unsigned>(hr));
        return false;
    }

    DXGI_ADAPTER_DESC adapterDesc = {};
    if (SUCCEEDED(m_adapter->GetDesc(&adapterDesc)))
    {
        char name[128] = {};
        WideCharToMultiByte(CP_UTF8, 0, adapterDesc.Description, -1, name, sizeof(name), nullptr, nullptr);
        QuadBufferLog::Info("GPU: %s (vendor 0x%04X)", name, adapterDesc.VendorId);
    }

    QueryStereoSupport();
    QueryUnitySwapChain();

    // Microsoft: an app that renders in stereo must register for stereo status changes,
    // because the user can turn stereo on or off at any time.
    m_stereoStatusEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    hr = m_stereoStatusEvent
        ? m_factory->RegisterStereoStatusEvent(m_stereoStatusEvent, &m_stereoStatusCookie)
        : HRESULT_FROM_WIN32(GetLastError());
    if (SUCCEEDED(hr))
    {
        QuadBufferLog::Info("Registered for stereo status notifications.");
    }
    else
    {
        SetError(hr);
        QuadBufferLog::Warning("RegisterStereoStatusEvent failed (hr=0x%08X). Stereo on/off changes "
                               "will not be detected while running.", static_cast<unsigned>(hr));
        if (m_stereoStatusEvent) { CloseHandle(m_stereoStatusEvent); m_stereoStatusEvent = nullptr; }
    }

    return true;
}

void D3D11StereoPresenter::Shutdown()
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_stereoStatusEvent)
    {
        if (m_factory)
            m_factory->UnregisterStereoStatus(m_stereoStatusCookie);
        CloseHandle(m_stereoStatusEvent);
        m_stereoStatusEvent = nullptr;
    }

    ReleaseSwapChain();
    SafeRelease(m_left);
    SafeRelease(m_right);
    SafeRelease(m_factory);
    SafeRelease(m_adapter);
    SafeRelease(m_context);
    m_device = nullptr;
    m_unityGraphics = nullptr;
    m_unitySwapEffect = -1;

    if (m_framesPresented > 0)
        QuadBufferLog::Info("Shut down after %lld stereo frames.", static_cast<long long>(m_framesPresented));
}

void D3D11StereoPresenter::SetWindow(HWND hwnd)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (hwnd == m_hwnd)
        return;

    m_hwnd = hwnd;
    ReleaseSwapChain();
    m_swapChainFailed = false;

    if (hwnd)
        QuadBufferLog::Info("Target window set (HWND 0x%p).", hwnd);
    else
        QuadBufferLog::Warning("Target window cleared (no HWND). Nothing will be presented.");
}

void D3D11StereoPresenter::SetEyeTextures(ID3D11Texture2D* left, ID3D11Texture2D* right)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (left)  left->AddRef();
    if (right) right->AddRef();
    SafeRelease(m_left);
    SafeRelease(m_right);
    m_left = left;
    m_right = right;
    m_eyeTexturesChecked = false;
    m_eyeTexturesValid = false;

    if (left && right)
    {
        D3D11_TEXTURE2D_DESC desc;
        left->GetDesc(&desc);
        QuadBufferLog::Info("Eye textures set: %ux%u, DXGI format %d.", desc.Width, desc.Height, desc.Format);
    }
    else if (left || right)
    {
        QuadBufferLog::Error("Only one eye texture was set. Both are required.");
    }
    else
    {
        QuadBufferLog::Info("Eye textures released.");
    }
}

void D3D11StereoPresenter::QueryStereoSupport()
{
    // The factory can go stale after display changes; use a fresh one in that case.
    IDXGIFactory2* factory = m_factory;
    IDXGIFactory2* freshFactory = nullptr;
    if (!m_factory->IsCurrent() &&
        SUCCEEDED(CreateDXGIFactory1(__uuidof(IDXGIFactory2), reinterpret_cast<void**>(&freshFactory))))
    {
        factory = freshFactory;
    }

    // Windowed apps: IsWindowedStereoEnabled.
    m_windowedStereoEnabled = factory->IsWindowedStereoEnabled() == TRUE;
    SafeRelease(freshFactory);

    // Full-screen apps: stereo modes from GetDisplayModeList1 with DXGI_ENUM_MODES_STEREO.
    m_fullscreenStereoModeCount = 0;
    IDXGIOutput* output = nullptr;
    for (UINT i = 0; m_adapter->EnumOutputs(i, &output) != DXGI_ERROR_NOT_FOUND; ++i)
    {
        IDXGIOutput1* output1 = nullptr;
        if (SUCCEEDED(output->QueryInterface(__uuidof(IDXGIOutput1), reinterpret_cast<void**>(&output1))))
        {
            UINT count = 0;
            if (SUCCEEDED(output1->GetDisplayModeList1(DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_ENUM_MODES_STEREO, &count, nullptr)) && count > 0)
            {
                std::vector<DXGI_MODE_DESC1> modes(count);
                if (SUCCEEDED(output1->GetDisplayModeList1(DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_ENUM_MODES_STEREO, &count, modes.data())))
                {
                    for (UINT m = 0; m < count; ++m)
                        if (modes[m].Stereo)
                            ++m_fullscreenStereoModeCount;
                }
            }
            SafeRelease(output1);
        }
        SafeRelease(output);
    }

    QuadBufferLog::Info("Stereo support: windowed stereo enabled = %s, full-screen stereo modes = %d.",
                        YesNo(m_windowedStereoEnabled), m_fullscreenStereoModeCount);
    if (!m_windowedStereoEnabled)
    {
        QuadBufferLog::Warning("Windowed stereo is disabled. Enable stereo 3D in the GPU driver "
                               "(and Windows display settings, if offered) to use quad buffering.");
    }
}

void D3D11StereoPresenter::QueryUnitySwapChain()
{
    if (m_unitySwapEffect != -1 || !m_unityGraphics)
        return;

    // Unity may create its swap chain after the device, so this is retried until it exists.
    // The pointer is borrowed from Unity: no AddRef/Release.
    IDXGISwapChain* unitySwapChain = m_unityGraphics->GetSwapChain();
    if (!unitySwapChain)
        return;

    DXGI_SWAP_CHAIN_DESC desc = {};
    if (FAILED(unitySwapChain->GetDesc(&desc)))
        return;

    m_unitySwapEffect = desc.SwapEffect;
    if (IsFlipModel(m_unitySwapEffect))
    {
        QuadBufferLog::Warning("Unity's swap chain uses %s. DXGI allows one flip-model swap chain per window, "
                               "so the stereo swap chain will be refused on Unity's window. Turn off Player Settings > "
                               "Resolution and Presentation > \"Use DXGI flip model swapchain for D3D11\".",
                               SwapEffectName(m_unitySwapEffect));
    }
    else
    {
        QuadBufferLog::Info("Unity's swap chain uses %s, so the stereo swap chain can share Unity's window.",
                            SwapEffectName(m_unitySwapEffect));
    }
}

void D3D11StereoPresenter::HandleStereoStatusChange()
{
    if (!m_stereoStatusEvent || WaitForSingleObject(m_stereoStatusEvent, 0) != WAIT_OBJECT_0)
        return;

    ++m_stereoStatusChangeCount;
    const bool wasEnabled = m_windowedStereoEnabled;
    QuadBufferLog::Info("Stereo status notification received (#%d). Checking stereo support again.",
                        m_stereoStatusChangeCount);

    // The notification does not say what changed, so query the current state.
    QueryStereoSupport();

    if (wasEnabled && !m_windowedStereoEnabled)
    {
        QuadBufferLog::Warning("Stereo was turned off. Releasing the stereo swap chain.");
        ReleaseSwapChain();
    }
    else if (!wasEnabled && m_windowedStereoEnabled)
    {
        QuadBufferLog::Info("Stereo was turned on. The stereo swap chain will be created on the next frame.");
    }
    m_swapChainFailed = false;
}

bool D3D11StereoPresenter::CreateSwapChain(UINT width, UINT height)
{
    // Unity already owns a swap chain on this window. DXGI allows only one flip-model swap
    // chain per window, and a stereo swap chain must be flip-model, so this only succeeds when
    // Unity uses the blit model ("Use DXGI flip model swapchain for D3D11" off in Player
    // Settings). The alternative is a child window of our own covering Unity's client area.
    if (!m_hwnd)
    {
        QuadBufferLog::Error("Cannot create the stereo swap chain: no target window.");
        return false;
    }

    DXGI_SWAP_CHAIN_DESC1 desc = {};
    desc.Width = width;
    desc.Height = height;
    desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    desc.Stereo = TRUE;
    desc.SampleDesc.Count = 1;
    desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount = 2;
    desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;

    HRESULT hr = m_factory->CreateSwapChainForHwnd(m_device, m_hwnd, &desc, nullptr, nullptr, &m_swapChain);
    if (FAILED(hr))
    {
        SetError(hr);
        QuadBufferLog::Error("CreateSwapChainForHwnd (stereo, %ux%u) failed: hr=0x%08X (%s). Unity's swap chain: %s.",
                             width, height, static_cast<unsigned>(hr), DescribeHResult(hr),
                             m_unitySwapEffect == -1 ? "unknown" : SwapEffectName(m_unitySwapEffect));
        if (IsFlipModel(m_unitySwapEffect))
            QuadBufferLog::Error("Turn off \"Use DXGI flip model swapchain for D3D11\" in Player Settings and rebuild.");
        return false;
    }

    DXGI_SWAP_CHAIN_DESC1 created = {};
    m_swapChain->GetDesc1(&created);
    m_swapChainIsStereo = created.Stereo == TRUE;
    if (m_swapChainIsStereo)
        QuadBufferLog::Info("Created stereo swap chain %ux%u.", created.Width, created.Height);
    else
        QuadBufferLog::Error("Swap chain was created but is not stereo. The driver ignored the stereo request.");
    return true;
}

bool D3D11StereoPresenter::ValidateEyeTextures()
{
    if (m_eyeTexturesChecked)
        return m_eyeTexturesValid;
    m_eyeTexturesChecked = true;

    D3D11_TEXTURE2D_DESC l, r;
    m_left->GetDesc(&l);
    m_right->GetDesc(&r);

    DXGI_SWAP_CHAIN_DESC1 sc = {};
    m_swapChain->GetDesc1(&sc);

    m_eyeTexturesValid = false;
    if (l.Width != r.Width || l.Height != r.Height || l.Format != r.Format)
        QuadBufferLog::Error("Left (%ux%u fmt %d) and right (%ux%u fmt %d) eye textures differ.",
                             l.Width, l.Height, l.Format, r.Width, r.Height, r.Format);
    else if (l.Width != sc.Width || l.Height != sc.Height)
        QuadBufferLog::Error("Eye textures (%ux%u) do not match the swap chain (%ux%u).",
                             l.Width, l.Height, sc.Width, sc.Height);
    else if (!IsBackBufferCompatible(l.Format))
        QuadBufferLog::Error("Eye texture format %d cannot be copied to the R8G8B8A8 back buffer. "
                             "Use an ARGB32 render texture.", l.Format);
    else if (l.SampleDesc.Count != 1 || l.ArraySize != 1)
        QuadBufferLog::Error("Eye textures must not be multisampled or arrays.");
    else
        m_eyeTexturesValid = true;

    return m_eyeTexturesValid;
}

void D3D11StereoPresenter::ReleaseSwapChain()
{
    SafeRelease(m_swapChain);
    m_swapChainIsStereo = false;
    m_eyeTexturesChecked = false;
}

void D3D11StereoPresenter::SetError(HRESULT hr)
{
    m_lastError = hr;
}

void D3D11StereoPresenter::Present()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_device)
        return;

    QueryUnitySwapChain();
    HandleStereoStatusChange();

    if (!m_windowedStereoEnabled || !m_left || !m_right || m_swapChainFailed)
        return;

    if (!m_swapChain)
    {
        D3D11_TEXTURE2D_DESC eyeDesc;
        m_left->GetDesc(&eyeDesc);
        if (!CreateSwapChain(eyeDesc.Width, eyeDesc.Height))
        {
            // Don't retry every frame; a window or stereo status change clears this.
            m_swapChainFailed = true;
            return;
        }
    }

    if (!ValidateEyeTextures())
        return;

    ID3D11Texture2D* backBuffer = nullptr;
    HRESULT hr = m_swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBuffer));
    if (FAILED(hr))
    {
        SetError(hr);
        QuadBufferLog::Error("GetBuffer on the stereo swap chain failed: hr=0x%08X.", static_cast<unsigned>(hr));
        return;
    }

    // A stereo back buffer is a 2-slice texture array: slice 0 = left, slice 1 = right.
    m_context->CopySubresourceRegion(backBuffer, D3D11CalcSubresource(0, 0, 1), 0, 0, 0, m_left, 0, nullptr);
    m_context->CopySubresourceRegion(backBuffer, D3D11CalcSubresource(0, 1, 1), 0, 0, 0, m_right, 0, nullptr);
    SafeRelease(backBuffer);

    hr = m_swapChain->Present(1, 0);
    if (hr != m_lastPresentResult)
    {
        // Log changes only, so a persistent state doesn't flood the log every frame.
        if (FAILED(hr))
        {
            SetError(hr);
            QuadBufferLog::Error("Present failed: hr=0x%08X (%s).", static_cast<unsigned>(hr), DescribeHResult(hr));
        }
        else if (hr != S_OK)
        {
            QuadBufferLog::Warning("Present returned 0x%08X (%s).", static_cast<unsigned>(hr), DescribeHResult(hr));
        }
        else if (m_framesPresented > 0)
        {
            QuadBufferLog::Info("Present succeeded again.");
        }
        m_lastPresentResult = hr;
    }

    if (hr == S_OK && m_framesPresented++ == 0)
        QuadBufferLog::Info("First stereo frame presented. Quad buffering is active.");
}

void D3D11StereoPresenter::PollStatus()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (m_device)
    {
        QueryUnitySwapChain();
        HandleStereoStatusChange();
    }
}

void D3D11StereoPresenter::GetStatus(QuadBufferStatus& status)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    status.framesPresented = m_framesPresented;
    status.deviceInitialized = m_device != nullptr;
    status.windowedStereoEnabled = m_windowedStereoEnabled;
    status.fullscreenStereoModeCount = m_fullscreenStereoModeCount;
    status.stereoStatusRegistered = m_stereoStatusEvent != nullptr;
    status.stereoStatusChangeCount = m_stereoStatusChangeCount;
    status.hasWindow = m_hwnd != nullptr;
    status.hasEyeTextures = m_left != nullptr && m_right != nullptr;
    status.eyeTexturesValid = m_eyeTexturesValid;
    status.swapChainCreated = m_swapChain != nullptr;
    status.swapChainIsStereo = m_swapChainIsStereo;
    status.lastErrorHResult = static_cast<int32_t>(m_lastError);
    status.unitySwapEffect = m_unitySwapEffect;
}
