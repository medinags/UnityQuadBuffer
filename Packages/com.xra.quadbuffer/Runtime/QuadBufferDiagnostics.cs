using System;
using System.Text;
using UnityEngine;

namespace QuadBuffer
{
    /// <summary>
    /// Tracks and logs the quad-buffer state: logs the environment once, logs every
    /// <see cref="QuadBufferState"/> change with an explanation, and forwards native plugin
    /// messages to <see cref="NativeLogReceived"/>.
    /// </summary>
    /// <remarks>
    /// Runs only while a <see cref="QuadBufferManager"/> is enabled in the scene: the manager
    /// registers itself and drives <c>Tick</c> every frame. Without one, nothing runs and
    /// <see cref="IsRunning"/> is false; <see cref="QueryStatus"/> still works on demand.
    /// In the Editor, in development builds, or when the player is started with
    /// <c>-quadbuffer-console</c>, registering also adds a <see cref="QuadBufferConsole"/> overlay.
    /// </remarks>
    public static class QuadBufferDiagnostics
    {
        /// <summary>Command-line argument that adds the in-game console to any build.</summary>
        public const string ConsoleArgument = "-quadbuffer-console";

        /// <summary>Prefix of every log message written by the package's C# code.</summary>
        public const string LogPrefix = "[QuadBuffer]";

        /// <summary>Prefix of every log message written by the native plugin.</summary>
        public const string NativeLogPrefix = "[QuadBuffer native]";

        const float StallSeconds = 1f;

        static bool s_pluginMissing;
        static bool s_environmentLogged;
        static IntPtr s_renderEventFunc;
        static long s_lastFrames;
        static float s_lastFrameTime;
        static readonly byte[] s_logBuffer = new byte[1024];
        static QuadBufferManager s_manager;

        /// <summary>The manager driving the diagnostics, or <c>null</c> if none is enabled.</summary>
        public static QuadBufferManager Manager => s_manager;

        /// <summary>True while a <see cref="QuadBufferManager"/> is enabled and driving the diagnostics.</summary>
        public static bool IsRunning => s_manager != null;

        /// <summary>The most recent status, refreshed every frame while <see cref="IsRunning"/>.</summary>
        public static QuadBufferStatus Status { get; private set; }

        /// <summary>Raised when <see cref="QuadBufferStatus.State"/> changes.</summary>
        public static event Action<QuadBufferStatus> StatusChanged;

        /// <summary>Raised on the main thread for each message from the native plugin.</summary>
        public static event Action<string, LogType> NativeLogReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Needed when "Enter Play Mode Options" disables domain reload.
            s_pluginMissing = false;
            s_environmentLogged = false;
            s_renderEventFunc = IntPtr.Zero;
            s_lastFrames = 0;
            s_lastFrameTime = 0f;
            s_manager = null;
            Status = default;
            StatusChanged = null;
            NativeLogReceived = null;
        }

        /// <summary>Called by <see cref="QuadBufferManager"/> when it is enabled.</summary>
        internal static void Register(QuadBufferManager manager)
        {
            if (s_manager == manager)
                return;
            if (s_manager != null)
            {
                Debug.LogWarning($"{LogPrefix} More than one QuadBufferManager is enabled ('{s_manager.name}' and " +
                                 $"'{manager.name}'). Only '{s_manager.name}' drives the diagnostics; use one per scene.");
                return;
            }

            s_manager = manager;

            // Created before anything is logged so the console shows the start-up messages.
            if (ShouldAddConsole(manager.Console) && UnityEngine.Object.FindAnyObjectByType<QuadBufferConsole>() == null)
                manager.gameObject.AddComponent<QuadBufferConsole>();

            LogEnvironment();
            Debug.Log($"{LogPrefix} Diagnostics started by QuadBufferManager '{manager.name}'.");
            Tick(manager);
        }

        /// <summary>Called by <see cref="QuadBufferManager"/> when it is disabled or destroyed.</summary>
        internal static void Unregister(QuadBufferManager manager)
        {
            if (s_manager != manager)
                return;
            s_manager = null;
            Status = default;
            Debug.Log($"{LogPrefix} Diagnostics stopped: QuadBufferManager '{manager.name}' was disabled or destroyed.");
        }

        static bool ShouldAddConsole(QuadBufferManager.ConsoleMode mode)
        {
            if (Application.isBatchMode || mode == QuadBufferManager.ConsoleMode.Never)
                return false;
            if (mode == QuadBufferManager.ConsoleMode.Always || Application.isEditor || Debug.isDebugBuild)
                return true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), ConsoleArgument) >= 0)
                return true;

            // Release build in Auto mode: say how to get the console, since F12 does nothing.
            Debug.Log($"{LogPrefix} In-game console not added (release build). Set the QuadBufferManager's " +
                      $"Console to Always, make a development build, or start the player with {ConsoleArgument}.");
            return false;
        }

        /// <summary>The platform, GPU and display, one item per entry, as logged at start-up.</summary>
        public static string[] DescribeEnvironmentLines()
        {
            return new[]
            {
                $"Unity {Application.unityVersion}, {SystemInfo.operatingSystem}",
                $"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceVendor}), driver: {SystemInfo.graphicsDeviceVersion}",
                $"Graphics API: {SystemInfo.graphicsDeviceType}, color space: {QualitySettings.activeColorSpace}",
                $"Screen: {Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio.value:0.##} Hz, {Screen.fullScreenMode}",
                $"Log file: {(string.IsNullOrEmpty(Application.consoleLogPath) ? "(none)" : Application.consoleLogPath)}",
            };
        }

        /// <summary>A multi-line description of the platform, GPU and display, as logged at start-up.</summary>
        /// <remarks>Lines are separated by <c>\n</c> only: IMGUI draws a <c>\r</c> as a return to the line start.</remarks>
        public static string DescribeEnvironment() => string.Join("\n", DescribeEnvironmentLines());

        static void LogEnvironment()
        {
            if (s_environmentLogged)
                return;
            s_environmentLogged = true;
            Debug.Log($"{LogPrefix} Environment\n{DescribeEnvironment()}");
        }

        /// <summary>Reads the current status from the native plugin.</summary>
        public static QuadBufferStatus QueryStatus()
        {
            var status = new QuadBufferStatus();
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                status.State = QuadBufferState.UnsupportedPlatform;
                status.Renderer = SystemInfo.graphicsDeviceType.ToString();
                return status;
            }

            if (!TryGetNativeStatus(out var native))
            {
                status.State = QuadBufferState.PluginMissing;
                status.Renderer = SystemInfo.graphicsDeviceType.ToString();
                return status;
            }

            status.Renderer = RendererName(native.renderer);
            status.DeviceInitialized = native.deviceInitialized != 0;
            status.WindowedStereoEnabled = native.windowedStereoEnabled != 0;
            status.FullscreenStereoModeCount = native.fullscreenStereoModeCount;
            status.StereoStatusRegistered = native.stereoStatusRegistered != 0;
            status.StereoStatusChangeCount = native.stereoStatusChangeCount;
            status.HasWindow = native.hasWindow != 0;
            status.HasEyeTextures = native.hasEyeTextures != 0;
            status.EyeTexturesValid = native.eyeTexturesValid != 0;
            status.SwapChainCreated = native.swapChainCreated != 0;
            status.SwapChainIsStereo = native.swapChainIsStereo != 0;
            status.FramesPresented = native.framesPresented;
            status.LastErrorHResult = native.lastErrorHResult;
            status.UnitySwapChainModel = ToSwapChainModel(native.unitySwapEffect);
            status.State = DetermineState(native, status.UnitySwapChainModel);
            return status;
        }

        static SwapChainModel ToSwapChainModel(int dxgiSwapEffect)
        {
            switch (dxgiSwapEffect)
            {
                case 0: // DXGI_SWAP_EFFECT_DISCARD
                case 1: // DXGI_SWAP_EFFECT_SEQUENTIAL
                    return SwapChainModel.Blit;
                case 3: // DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL
                case 4: // DXGI_SWAP_EFFECT_FLIP_DISCARD
                    return SwapChainModel.Flip;
                default:
                    return SwapChainModel.Unknown;
            }
        }

        static QuadBufferState DetermineState(QuadBufferNative.Status s, SwapChainModel unitySwapChain)
        {
            const int kD3D11 = 2;
            if (s.renderer != kD3D11) return QuadBufferState.WrongGraphicsApi;
            if (s.deviceInitialized == 0) return QuadBufferState.DeviceNotReady;
            if (s.windowedStereoEnabled == 0) return QuadBufferState.StereoDisabled;
            // The Editor's swap chain doesn't follow Player Settings, so only judge it in players.
            if (unitySwapChain == SwapChainModel.Flip && !Application.isEditor) return QuadBufferState.FlipModelSwapChain;
            if (s.hasEyeTextures == 0) return QuadBufferState.Idle;
            if (s.hasWindow == 0) return QuadBufferState.NoWindow;
            if (s.swapChainCreated == 0) return s.lastErrorHResult != 0 ? QuadBufferState.SwapChainFailed : QuadBufferState.Starting;
            if (s.swapChainIsStereo == 0) return QuadBufferState.SwapChainNotStereo;
            if (s.eyeTexturesValid == 0) return QuadBufferState.InvalidEyeTextures;
            if (s.framesPresented == 0) return QuadBufferState.Starting;
            return QuadBufferState.Presenting;
        }

        static bool TryGetNativeStatus(out QuadBufferNative.Status native)
        {
            native = default;
            if (s_pluginMissing)
                return false;
            try
            {
                QuadBufferNative.QuadBuffer_GetStatus(out native);
                return true;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                s_pluginMissing = true;
                Debug.LogError($"{LogPrefix} Could not load the native plugin: {e.Message}");
                return false;
            }
        }

        static string RendererName(int renderer)
        {
            switch (renderer)
            {
                case 2: return "Direct3D11";
                case 4: return "Null";
                case 17: return "OpenGLCore";
                case 18: return "Direct3D12";
                case 21: return "Vulkan";
                default: return $"Renderer {renderer}";
            }
        }

        /// <summary>Called every frame by the registered <see cref="QuadBufferManager"/>.</summary>
        internal static void Tick(QuadBufferManager caller)
        {
            // Nothing runs without a registered manager; a second manager doesn't tick twice.
            if (s_manager == null || caller != s_manager)
                return;

            PumpNativeLog();

            var status = QueryStatus();

            // Presenting but no new frame for a while: the window is hidden or Present fails.
            if (status.FramesPresented != s_lastFrames)
            {
                s_lastFrames = status.FramesPresented;
                s_lastFrameTime = Time.unscaledTime;
            }
            else if (status.State == QuadBufferState.Presenting && Time.unscaledTime - s_lastFrameTime > StallSeconds)
            {
                status.State = QuadBufferState.Stalled;
            }

            var previous = Status;
            Status = status;
            if (previous.Renderer == null || previous.State != status.State)
                OnStateChanged(status);

            if (status.UnitySwapChainModel != previous.UnitySwapChainModel && status.UnitySwapChainModel != SwapChainModel.Unknown)
                LogSwapChainModel(status.UnitySwapChainModel);

            // Let the plugin handle stereo on/off notifications even when nothing is presenting.
            if (status.DeviceInitialized)
            {
                if (s_renderEventFunc == IntPtr.Zero)
                    s_renderEventFunc = QuadBufferNative.QuadBuffer_GetRenderEventFunc();
                GL.IssuePluginEvent(s_renderEventFunc, QuadBufferNative.EventPollStatus);
            }
        }

        static void LogSwapChainModel(SwapChainModel model)
        {
            if (Application.isEditor)
            {
                Debug.Log($"{LogPrefix} Editor swap chain model: {model}. The Editor ignores \"Use DXGI flip model " +
                          "swapchain for D3D11\"; check the model in a player build.");
            }
            else if (model == SwapChainModel.Flip)
            {
                Debug.LogWarning($"{LogPrefix} Unity swap chain model: Flip. The stereo swap chain can't share Unity's window. " +
                                 "Turn off \"Use DXGI flip model swapchain for D3D11\" in Player Settings and rebuild.");
            }
            else
            {
                Debug.Log($"{LogPrefix} Unity swap chain model: {model}. The stereo swap chain can share Unity's window.");
            }
        }

        static void OnStateChanged(QuadBufferStatus status)
        {
            var message = $"{LogPrefix} Status: {status.State}. {status.Explanation}";
            switch (status.State)
            {
                case QuadBufferState.Idle:
                case QuadBufferState.Starting:
                case QuadBufferState.Presenting:
                case QuadBufferState.DeviceNotReady:
                    Debug.Log(message);
                    break;
                case QuadBufferState.PluginMissing:
                case QuadBufferState.SwapChainFailed:
                case QuadBufferState.SwapChainNotStereo:
                case QuadBufferState.InvalidEyeTextures:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.LogWarning(message);
                    break;
            }
            StatusChanged?.Invoke(status);
        }

        static void PumpNativeLog()
        {
            if (s_pluginMissing)
                return;
            try
            {
                // Bounded so a flood can't stall a frame; the rest is read next frame.
                for (int i = 0; i < 64 && QuadBufferNative.QuadBuffer_PopLogMessage(out int type, s_logBuffer, s_logBuffer.Length); i++)
                {
                    int length = Array.IndexOf(s_logBuffer, (byte)0);
                    var text = Encoding.UTF8.GetString(s_logBuffer, 0, length < 0 ? s_logBuffer.Length : length);
                    NativeLogReceived?.Invoke(text, ToLogType(type));
                }
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                // Reported by TryGetNativeStatus.
            }
        }

        static LogType ToLogType(int unityLogType)
        {
            switch (unityLogType)
            {
                case 0: return LogType.Error;
                case 2: return LogType.Warning;
                case 4: return LogType.Exception;
                default: return LogType.Log;
            }
        }
    }
}
