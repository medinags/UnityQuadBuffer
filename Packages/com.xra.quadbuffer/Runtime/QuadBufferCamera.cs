using System;
using System.Collections;
using UnityEngine;

namespace QuadBuffer
{
    /// <summary>
    /// Renders left/right eyes into render textures and hands them to the native
    /// plugin, which presents them through a quad-buffered stereo swap chain.
    /// </summary>
    /// <remarks>
    /// If stereo is not available when the component is enabled, it waits and starts as soon
    /// as the driver reports stereo; it stops again if stereo is turned off.
    /// Progress and problems are logged with the <c>[QuadBuffer]</c> prefix; see
    /// <see cref="QuadBufferDiagnostics"/> for the overall status.
    /// </remarks>
    [DisallowMultipleComponent]
    public class QuadBufferCamera : MonoBehaviour
    {
        /// <summary>Distance between the left and right eye cameras, in world units.</summary>
        [Tooltip("Distance between the eyes, in world units.")]
        public float eyeSeparation = 0.064f;

        Camera m_leftEye;
        Camera m_rightEye;
        RenderTexture m_leftTexture;
        RenderTexture m_rightTexture;
        bool m_supportedPlatform;
        bool m_waitingLogged;

        /// <summary>
        /// True while the plugin reports that it can present stereo
        /// (Windows player, Direct3D11, driver stereo enabled). False in the Editor.
        /// </summary>
        public bool IsStereoAvailable { get; private set; }

        /// <summary>True while the eye cameras exist and frames are sent to the plugin.</summary>
        public bool IsRendering => m_leftEye != null;

        void OnEnable()
        {
            m_supportedPlatform = Application.platform == RuntimePlatform.WindowsPlayer;
            if (!m_supportedPlatform)
            {
                Debug.LogWarning("[QuadBuffer] Stereo output only works in a Windows standalone player.");
                return;
            }

            m_waitingLogged = false;
            UpdateStereo(QuadBufferDiagnostics.QueryStatus());
        }

        void Start()
        {
            // Checked in Start: every object's OnEnable (including the manager's) has run by now.
            if (m_supportedPlatform && !QuadBufferDiagnostics.IsRunning)
                Debug.LogWarning($"[QuadBuffer] '{name}': no QuadBufferManager in the scene. Stereo still works, but " +
                                 "diagnostics, the console and stereo on/off detection while waiting are off. " +
                                 "Add a QuadBufferManager.");
        }

        void OnDisable()
        {
            if (IsRendering)
                StopStereo("component disabled");
        }

        void Update()
        {
            if (!m_supportedPlatform)
                return;
            // The manager refreshes Status earlier in the frame; without one, ask the plugin directly.
            UpdateStereo(QuadBufferDiagnostics.IsRunning ? QuadBufferDiagnostics.Status : QuadBufferDiagnostics.QueryStatus());
        }

        void UpdateStereo(QuadBufferStatus status)
        {
            IsStereoAvailable = status.DeviceInitialized && status.WindowedStereoEnabled;

            if (IsStereoAvailable && !IsRendering)
            {
                StartStereo();
            }
            else if (!IsStereoAvailable && IsRendering)
            {
                StopStereo($"stereo is no longer available ({status.State})");
            }
            else if (!IsStereoAvailable && !m_waitingLogged)
            {
                m_waitingLogged = true;
                Debug.LogWarning($"[QuadBuffer] '{name}': stereo is not available ({status.State}). {status.Explanation} " +
                                 "Rendering stays mono; stereo starts automatically if it becomes available.");
            }
        }

        void LateUpdate()
        {
            if (!IsRendering)
                return;

            m_leftEye.transform.localPosition = new Vector3(-0.5f * eyeSeparation, 0f, 0f);
            m_rightEye.transform.localPosition = new Vector3(0.5f * eyeSeparation, 0f, 0f);
        }

        void StartStereo()
        {
            m_leftTexture = CreateEyeTexture("QuadBuffer Left");
            m_rightTexture = CreateEyeTexture("QuadBuffer Right");
            m_leftEye = CreateEyeCamera("Left Eye", -0.5f, m_leftTexture);
            m_rightEye = CreateEyeCamera("Right Eye", 0.5f, m_rightTexture);

            var hwnd = QuadBufferNative.GetActiveWindow();
            if (hwnd == IntPtr.Zero)
                Debug.LogWarning("[QuadBuffer] Could not find the player window (GetActiveWindow returned null). " +
                                 "Is the window focused?");
            QuadBufferNative.QuadBuffer_SetWindow(hwnd);
            QuadBufferNative.QuadBuffer_SetEyeTextures(
                m_leftTexture.GetNativeTexturePtr(), m_rightTexture.GetNativeTexturePtr());

            StartCoroutine(PresentLoop());
            Debug.Log($"[QuadBuffer] '{name}': stereo rendering started. Eye textures {m_leftTexture.width}x{m_leftTexture.height}, " +
                      $"eye separation {eyeSeparation}.");
        }

        void StopStereo(string reason)
        {
            StopAllCoroutines();
            QuadBufferNative.QuadBuffer_SetEyeTextures(IntPtr.Zero, IntPtr.Zero);
            DestroyEye(ref m_leftEye, ref m_leftTexture);
            DestroyEye(ref m_rightEye, ref m_rightTexture);
            m_waitingLogged = false;
            Debug.Log($"[QuadBuffer] '{name}': stereo rendering stopped: {reason}.");
        }

        IEnumerator PresentLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            var renderEvent = QuadBufferNative.QuadBuffer_GetRenderEventFunc();
            while (true)
            {
                yield return endOfFrame;
                GL.IssuePluginEvent(renderEvent, QuadBufferNative.EventPresent);
            }
        }

        RenderTexture CreateEyeTexture(string textureName)
        {
            // TODO: recreate on screen resolution change.
            var rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = textureName
            };
            rt.Create();
            return rt;
        }

        Camera CreateEyeCamera(string eyeName, float side, RenderTexture target)
        {
            var source = GetComponent<Camera>();
            var go = new GameObject(eyeName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(side * eyeSeparation, 0f, 0f);

            var cam = go.AddComponent<Camera>();
            if (source != null)
                cam.CopyFrom(source);
            cam.targetTexture = target;
            return cam;
        }

        static void DestroyEye(ref Camera cam, ref RenderTexture rt)
        {
            if (cam != null) Destroy(cam.gameObject);
            if (rt != null) { rt.Release(); Destroy(rt); }
            cam = null;
            rt = null;
        }
    }
}
