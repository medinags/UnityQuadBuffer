namespace QuadBuffer
{
    /// <summary>Overall quad-buffer state, from "cannot work here" to "presenting stereo".</summary>
    public enum QuadBufferState
    {
        /// <summary>Not running on Windows. Quad buffering is Windows-only.</summary>
        UnsupportedPlatform,
        /// <summary>QuadBufferPlugin.dll could not be loaded.</summary>
        PluginMissing,
        /// <summary>Unity is not using Direct3D11.</summary>
        WrongGraphicsApi,
        /// <summary>The plugin has not bound to Unity's graphics device yet.</summary>
        DeviceNotReady,
        /// <summary>The OS or GPU driver has stereo turned off (IsWindowedStereoEnabled is false).</summary>
        StereoDisabled,
        /// <summary>
        /// Unity's swap chain uses the DXGI flip model, so its window can't take the stereo swap chain.
        /// Only reported in players; the Editor's own swap chain doesn't reflect Player Settings.
        /// </summary>
        FlipModelSwapChain,
        /// <summary>Stereo is supported, but no camera is sending eye images (no camera found or set on the manager).</summary>
        Idle,
        /// <summary>Eye images are set but no target window is.</summary>
        NoWindow,
        /// <summary>Creating the stereo swap chain failed. See <see cref="QuadBufferStatus.LastErrorHResult"/>.</summary>
        SwapChainFailed,
        /// <summary>A swap chain was created, but the driver did not make it stereo.</summary>
        SwapChainNotStereo,
        /// <summary>The eye textures do not match each other or the swap chain.</summary>
        InvalidEyeTextures,
        /// <summary>Everything is set up; waiting for the first stereo frame.</summary>
        Starting,
        /// <summary>Stereo frames are being presented.</summary>
        Presenting,
        /// <summary>Stereo frames were being presented but no new frame arrived for over a second.</summary>
        Stalled,
    }

    /// <summary>Presentation model of Unity's own DXGI swap chain.</summary>
    public enum SwapChainModel
    {
        /// <summary>Not known yet (Unity's swap chain doesn't exist yet, or the plugin isn't bound).</summary>
        Unknown,
        /// <summary>Blit model (DISCARD / SEQUENTIAL). The stereo swap chain can share Unity's window.</summary>
        Blit,
        /// <summary>Flip model (FLIP_SEQUENTIAL / FLIP_DISCARD). Blocks a second swap chain on Unity's window.</summary>
        Flip,
    }

    /// <summary>Snapshot of the quad-buffer state. Get one from <see cref="QuadBufferDiagnostics.Status"/>.</summary>
    public struct QuadBufferStatus
    {
        /// <summary>Overall state.</summary>
        public QuadBufferState State;
        /// <summary>Graphics API the native plugin saw, e.g. "Direct3D11".</summary>
        public string Renderer;
        /// <summary>The plugin is bound to Unity's Direct3D11 device.</summary>
        public bool DeviceInitialized;
        /// <summary>The OS and driver allow windowed stereo (<c>IDXGIFactory2::IsWindowedStereoEnabled</c>).</summary>
        public bool WindowedStereoEnabled;
        /// <summary>Stereo full-screen display modes found on the GPU's outputs.</summary>
        public int FullscreenStereoModeCount;
        /// <summary>The plugin is registered for stereo on/off notifications.</summary>
        public bool StereoStatusRegistered;
        /// <summary>Stereo on/off notifications received since start-up.</summary>
        public int StereoStatusChangeCount;
        /// <summary>A target window is set.</summary>
        public bool HasWindow;
        /// <summary>Left and right eye textures are set.</summary>
        public bool HasEyeTextures;
        /// <summary>The eye textures passed validation against the swap chain.</summary>
        public bool EyeTexturesValid;
        /// <summary>The stereo swap chain exists.</summary>
        public bool SwapChainCreated;
        /// <summary>The swap chain is stereo.</summary>
        public bool SwapChainIsStereo;
        /// <summary>Stereo frames presented since start-up.</summary>
        public long FramesPresented;
        /// <summary>Most recent failing HRESULT from the plugin, 0 if none.</summary>
        public int LastErrorHResult;
        /// <summary>Presentation model of Unity's own swap chain.</summary>
        public SwapChainModel UnitySwapChainModel;

        /// <summary>True when stereo frames are reaching the display.</summary>
        public bool IsWorking => State == QuadBufferState.Presenting;

        /// <summary>One-sentence explanation of <see cref="State"/> with what to do about it.</summary>
        public string Explanation => Explain(State, LastErrorHResult);

        internal static string Explain(QuadBufferState state, int hr)
        {
            switch (state)
            {
                case QuadBufferState.UnsupportedPlatform: return "Quad buffering only works on Windows.";
                case QuadBufferState.PluginMissing:       return "QuadBufferPlugin.dll is missing or failed to load. Check the plugin's import settings (Windows x86_64).";
                case QuadBufferState.WrongGraphicsApi:    return "Unity is not using Direct3D11. Set Graphics APIs for Windows to Direct3D11 only.";
                case QuadBufferState.DeviceNotReady:      return "The plugin has not bound to the graphics device yet.";
                case QuadBufferState.StereoDisabled:      return "Stereo is turned off in the OS or GPU driver. Enable stereo 3D in the driver control panel.";
                case QuadBufferState.FlipModelSwapChain:  return "Unity uses a flip-model swap chain, which blocks the stereo swap chain. Turn off Player Settings > Resolution and Presentation > \"Use DXGI flip model swapchain for D3D11\" and rebuild.";
                case QuadBufferState.Idle:                return "Stereo is supported, but no camera is sending eye images. Assign a camera on the QuadBufferManager or call SetCamera.";
                case QuadBufferState.NoWindow:            return "No target window. The player window handle could not be found.";
                case QuadBufferState.SwapChainFailed:     return $"Creating the stereo swap chain failed (HRESULT 0x{hr:X8}). See the log for details.";
                case QuadBufferState.SwapChainNotStereo:  return "The driver created a mono swap chain. Check the driver's stereo settings.";
                case QuadBufferState.InvalidEyeTextures:  return "The eye textures don't match the swap chain. See the log for details.";
                case QuadBufferState.Starting:            return "Waiting for the first stereo frame.";
                case QuadBufferState.Presenting:          return "Quad buffering is active.";
                case QuadBufferState.Stalled:             return "No stereo frame for over a second (window hidden, minimized, or Present failing).";
                default:                                  return state.ToString();
            }
        }
    }
}
