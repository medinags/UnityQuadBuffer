using UnityEngine;

namespace QuadBuffer
{
    /// <summary>
    /// Entry point for quad-buffer stereo in a scene. Puts a <see cref="QuadBufferCamera"/> on the
    /// stereo camera and runs <see cref="QuadBufferDiagnostics"/> (status, logs, console).
    /// </summary>
    /// <remarks>
    /// Camera selection, in order:
    /// <list type="number">
    /// <item><description>The camera assigned in the Inspector (<see cref="TargetCamera"/>).</description></item>
    /// <item><description>Otherwise, at start-up: a camera that already has a <see cref="QuadBufferCamera"/>,
    /// then <c>Camera.main</c>, then the first enabled camera that renders to the screen.</description></item>
    /// <item><description>Otherwise, the camera passed to <see cref="SetCamera"/> by another script.</description></item>
    /// </list>
    /// Use one manager per scene. Diagnostics only run while a manager is enabled.
    /// </remarks>
    [AddComponentMenu("QuadBuffer/Quad Buffer Manager")]
    [DisallowMultipleComponent]
    // Runs before other scripts so QuadBufferCamera.Update reads this frame's status.
    [DefaultExecutionOrder(-1000)]
    public class QuadBufferManager : MonoBehaviour
    {
        const string LogPrefix = QuadBufferDiagnostics.LogPrefix;

        /// <summary>When the manager adds the in-game <see cref="QuadBufferConsole"/>.</summary>
        public enum ConsoleMode
        {
            /// <summary>In the Editor, in development builds, and with the <c>-quadbuffer-console</c> argument.</summary>
            Auto,
            /// <summary>In every build, including release builds.</summary>
            Always,
            /// <summary>Never. A console added to the scene by hand still works.</summary>
            Never,
        }

        [Tooltip("Camera to render in stereo. If empty, one is searched for at start-up.")]
        [SerializeField] Camera targetCamera;

        [Tooltip("When to add the in-game console (F12). Auto: Editor, development builds and " +
                 "-quadbuffer-console. Always: also release builds.")]
        [SerializeField] ConsoleMode console = ConsoleMode.Auto;

        /// <summary>When the in-game console is added. Read when the manager is enabled.</summary>
        public ConsoleMode Console
        {
            get => console;
            set => console = value;
        }

        QuadBufferCamera m_stereoCamera;
        bool m_addedStereoCamera;

        /// <summary>The camera rendered in stereo, or <c>null</c> if none has been found or set yet.</summary>
        public Camera TargetCamera => targetCamera;

        /// <summary>The <see cref="QuadBufferCamera"/> on <see cref="TargetCamera"/>, or <c>null</c>.</summary>
        public QuadBufferCamera StereoCamera => m_stereoCamera;

        void OnEnable()
        {
            QuadBufferDiagnostics.Register(this);
        }

        void OnDisable()
        {
            QuadBufferDiagnostics.Unregister(this);
        }

        void Start()
        {
            // SetCamera may already have been called by another script's Awake/OnEnable.
            if (m_stereoCamera != null)
                return;

            if (targetCamera != null)
            {
                Debug.Log($"{LogPrefix} Manager '{name}': using the camera assigned in the Inspector, '{targetCamera.name}'.");
                Attach(targetCamera);
                return;
            }

            var found = FindStereoCamera(out var how);
            if (found != null)
            {
                Debug.Log($"{LogPrefix} Manager '{name}': no camera assigned; using '{found.name}' ({how}).");
                Attach(found);
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} Manager '{name}': no camera assigned and none found in the scene. " +
                                 "Stereo stays off until a script calls QuadBufferManager.SetCamera(camera).");
            }
        }

        void Update()
        {
            QuadBufferDiagnostics.Tick(this);
        }

        /// <summary>
        /// Sets the camera to render in stereo and adds a <see cref="QuadBufferCamera"/> to it.
        /// Can be called at any time, including before <c>Start</c>. The previous camera stops stereo.
        /// </summary>
        /// <param name="newCamera">The camera to use. <c>null</c> is rejected with an error.</param>
        public void SetCamera(Camera newCamera)
        {
            if (newCamera == null)
            {
                Debug.LogError($"{LogPrefix} Manager '{name}': SetCamera(null) ignored. Pass the camera to render in stereo.");
                return;
            }

            if (newCamera == targetCamera && m_stereoCamera != null)
            {
                Debug.Log($"{LogPrefix} Manager '{name}': '{newCamera.name}' is already the stereo camera.");
                return;
            }

            Debug.Log($"{LogPrefix} Manager '{name}': camera set by script to '{newCamera.name}'.");
            Attach(newCamera);
        }

        void Attach(Camera newCamera)
        {
            Detach();
            targetCamera = newCamera;

            m_stereoCamera = newCamera.GetComponent<QuadBufferCamera>();
            m_addedStereoCamera = m_stereoCamera == null;
            if (m_addedStereoCamera)
            {
                m_stereoCamera = newCamera.gameObject.AddComponent<QuadBufferCamera>();
                Debug.Log($"{LogPrefix} Manager '{name}': added QuadBufferCamera to '{newCamera.name}'.");
            }
            else
            {
                m_stereoCamera.enabled = true;
                Debug.Log($"{LogPrefix} Manager '{name}': '{newCamera.name}' already has a QuadBufferCamera; using it.");
            }
        }

        // Stops stereo on the previous camera: removes the component if the manager added it,
        // otherwise disables it so two cameras never send eye textures to the plugin.
        void Detach()
        {
            if (m_stereoCamera == null)
                return;

            var previous = m_stereoCamera.name;
            // Disable first: OnDisable stops stereo now. Destroy alone is deferred to the end of
            // the frame and would clear the new camera's eye textures in the plugin.
            m_stereoCamera.enabled = false;
            if (m_addedStereoCamera)
            {
                Destroy(m_stereoCamera);
                Debug.Log($"{LogPrefix} Manager '{name}': removed QuadBufferCamera from '{previous}'.");
            }
            else
            {
                m_stereoCamera.enabled = false;
                Debug.Log($"{LogPrefix} Manager '{name}': disabled the QuadBufferCamera on '{previous}'.");
            }
            m_stereoCamera = null;
        }

        static Camera FindStereoCamera(out string how)
        {
            var existing = FindAnyObjectByType<QuadBufferCamera>();
            if (existing != null && existing.TryGetComponent(out Camera existingCamera))
            {
                how = "it already has a QuadBufferCamera";
                return existingCamera;
            }

            if (Camera.main != null)
            {
                how = "Camera.main";
                return Camera.main;
            }

            foreach (var candidate in FindObjectsByType<Camera>())
            {
                if (candidate.isActiveAndEnabled && candidate.targetTexture == null)
                {
                    how = "first enabled camera rendering to the screen";
                    return candidate;
                }
            }

            how = null;
            return null;
        }
    }
}
