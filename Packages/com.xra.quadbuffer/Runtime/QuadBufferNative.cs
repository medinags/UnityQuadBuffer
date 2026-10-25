using System;
using System.Runtime.InteropServices;

namespace QuadBuffer
{
    /// <summary>P/Invoke bindings for NativePlugin/include/QuadBufferPlugin.h.</summary>
    internal static class QuadBufferNative
    {
        const string Dll = "QuadBufferPlugin";

        /// <summary>Must match QuadBufferEvent in the native plugin.</summary>
        public const int EventPresent = 1;

        /// <summary>Must match QuadBufferEvent in the native plugin.</summary>
        public const int EventPollStatus = 2;

        /// <summary>Must match QuadBufferStatus in QuadBufferPlugin.h, field for field.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Status
        {
            public long framesPresented;
            public int renderer;
            public int deviceInitialized;
            public int windowedStereoEnabled;
            public int fullscreenStereoModeCount;
            public int stereoStatusRegistered;
            public int stereoStatusChangeCount;
            public int hasWindow;
            public int hasEyeTextures;
            public int eyeTexturesValid;
            public int swapChainCreated;
            public int swapChainIsStereo;
            public int lastErrorHResult;
            public int unitySwapEffect;
        }

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool QuadBuffer_IsStereoAvailable();

        [DllImport(Dll)]
        public static extern void QuadBuffer_SetWindow(IntPtr hwnd);

        [DllImport(Dll)]
        public static extern void QuadBuffer_SetEyeTextures(IntPtr left, IntPtr right);

        [DllImport(Dll)]
        public static extern IntPtr QuadBuffer_GetRenderEventFunc();

        [DllImport(Dll)]
        public static extern void QuadBuffer_GetStatus(out Status status);

        [DllImport(Dll)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool QuadBuffer_PopLogMessage(out int type, byte[] buffer, int bufferSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();
    }
}
