using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuadBuffer.Editor
{
    /// <summary>
    /// Warns when a Windows build isn't set up for quad buffering: Direct3D11 must be the graphics
    /// API, and the DXGI flip model swap chain must be off. Only warns; never changes settings.
    /// </summary>
    class QuadBufferBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                return;

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64) || apis.Length == 0 ||
                apis[0] != GraphicsDeviceType.Direct3D11)
            {
                Debug.LogWarning("[QuadBuffer] Set Player Settings > Graphics APIs for Windows to Direct3D11 only " +
                                 "(disable Auto Graphics API). Quad-buffer stereo is not available on other APIs.");
            }

            if (PlayerSettings.useFlipModelSwapchain)
            {
                Debug.LogWarning("[QuadBuffer] \"Use DXGI flip model swapchain for D3D11\" is on (Player Settings > " +
                                 "Resolution and Presentation). DXGI allows one flip-model swap chain per window, so the " +
                                 "stereo swap chain will be refused on Unity's window. Turn the setting off for quad buffering.");
            }
        }
    }
}
