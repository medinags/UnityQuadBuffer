using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace QuadBuffer
{
    /// <summary>
    /// In-game overlay that shows the quad-buffer status and the Unity log, including native
    /// plugin messages. Toggle it with <see cref="toggleKey"/> (F12 by default).
    /// </summary>
    /// <remarks>
    /// Added automatically in the Editor, in development builds, and when the player is started
    /// with <c>-quadbuffer-console</c> (see <see cref="QuadBufferDiagnostics"/>). Add it to a
    /// scene yourself to have it in release builds.
    /// </remarks>
    [AddComponentMenu("QuadBuffer/Quad Buffer Console")]
    public class QuadBufferConsole : MonoBehaviour
    {
        /// <summary>Key that shows or hides the console.</summary>
        [Tooltip("Key that shows or hides the console.")]
        public KeyCode toggleKey = KeyCode.F12;

        /// <summary>Show the console when the player starts.</summary>
        [Tooltip("Show the console when the player starts.")]
        public bool showOnStart;

        /// <summary>Show a one-line status badge in the corner while the console is hidden.</summary>
        [Tooltip("Show a one-line status badge in the corner while the console is hidden.")]
        public bool showStatusBadge = true;

        /// <summary>Oldest messages are dropped beyond this count.</summary>
        [Tooltip("Oldest messages are dropped beyond this count.")]
        public int maxEntries = 500;

        struct Entry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public System.DateTime time; // When the message arrived (any thread)
        }

        readonly List<Entry> m_entries = new List<Entry>();
        readonly List<Entry> m_pending = new List<Entry>();
        readonly object m_pendingLock = new object();

        bool m_visible;
        bool m_showLog = true, m_showWarnings = true, m_showErrors = true;
        bool m_quadBufferOnly;
        bool m_showStackTraces;
        bool m_autoScroll = true;
        Vector2 m_scroll;
        string[] m_environment;

        GUIStyle m_entryStyle, m_boxStyle, m_badgeStyle;
        Texture2D m_background;

        void Awake()
        {
            m_visible = showOnStart;
        }

        void OnEnable()
        {
            Application.logMessageReceivedThreaded += OnUnityLog;
            QuadBufferDiagnostics.NativeLogReceived += OnNativeLog;
        }

        void OnDisable()
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            QuadBufferDiagnostics.NativeLogReceived -= OnNativeLog;
        }

        void OnDestroy()
        {
            if (m_background != null)
                Destroy(m_background);
        }

        // May be called from any thread.
        void OnUnityLog(string message, string stackTrace, LogType type)
        {
            // Native messages arrive through OnNativeLog; skip the copy Unity's log may forward.
            if (message.StartsWith(QuadBufferDiagnostics.NativeLogPrefix))
                return;
            Enqueue(message, stackTrace, type);
        }

        void OnNativeLog(string message, LogType type) => Enqueue(message, null, type);

        void Enqueue(string message, string stackTrace, LogType type)
        {
            // Stamped here, not when moved to the list in Update: start-up messages all arrive before the
            // first Update and would otherwise share one time. DateTime is thread-safe; Unity's Time is not.
            var entry = new Entry { message = message, stackTrace = stackTrace, type = type, time = System.DateTime.Now };
            lock (m_pendingLock)
                m_pending.Add(entry);
        }

        void Update()
        {
            lock (m_pendingLock)
            {
                if (m_pending.Count == 0)
                    return;
                m_entries.AddRange(m_pending);
                m_pending.Clear();
            }

            if (m_entries.Count > maxEntries)
                m_entries.RemoveRange(0, m_entries.Count - maxEntries);
            if (m_autoScroll)
                m_scroll.y = float.MaxValue;
        }

        void OnGUI()
        {
            // IMGUI events work with both the legacy Input Manager and the Input System package.
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == toggleKey)
            {
                m_visible = !m_visible;
                e.Use();
            }

            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.dpi / 96f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale, height = Screen.height / scale;

            if (!m_visible)
            {
                if (showStatusBadge)
                    DrawBadge(width);
                return;
            }

            var area = new Rect(8, 8, width - 16, height * 0.6f);
            GUI.DrawTexture(area, m_background);
            GUILayout.BeginArea(new Rect(area.x + 6, area.y + 6, area.width - 12, area.height - 12));
            DrawToolbar();
            DrawStatus();
            DrawLog();
            GUILayout.EndArea();
        }

        void DrawBadge(float width)
        {
            var status = QuadBufferDiagnostics.Status;
            var state = QuadBufferDiagnostics.IsRunning
                ? $"<color={StateColor(status.State)}>QuadBuffer: {status.State}</color>"
                : "<color=#FFD966>QuadBuffer: no manager</color>";
            var text = $"{state}  ({toggleKey}: console)";
            var size = m_badgeStyle.CalcSize(new GUIContent(text));
            var rect = new Rect(width - size.x - 12, 8, size.x + 8, size.y + 4);
            GUI.DrawTexture(rect, m_background);
            GUI.Label(new Rect(rect.x + 4, rect.y + 2, size.x, size.y), text, m_badgeStyle);
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Quad Buffer Console</b>  ({toggleKey} to hide)", m_entryStyle);
            GUILayout.FlexibleSpace();
            m_showLog = GUILayout.Toggle(m_showLog, $"Log ({Count(LogType.Log)})");
            m_showWarnings = GUILayout.Toggle(m_showWarnings, $"Warnings ({Count(LogType.Warning)})");
            m_showErrors = GUILayout.Toggle(m_showErrors, $"Errors ({Count(LogType.Error)})");
            m_quadBufferOnly = GUILayout.Toggle(m_quadBufferOnly, "QuadBuffer only");
            m_showStackTraces = GUILayout.Toggle(m_showStackTraces, "Stack traces");
            m_autoScroll = GUILayout.Toggle(m_autoScroll, "Auto-scroll");
            if (GUILayout.Button("Copy", GUILayout.Width(60)))
                GUIUtility.systemCopyBuffer = BuildReport();
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                m_entries.Clear();
            GUILayout.EndHorizontal();
        }

        void DrawStatus()
        {
            var s = QuadBufferDiagnostics.Status;
            GUILayout.BeginVertical(m_boxStyle);
            if (!QuadBufferDiagnostics.IsRunning)
            {
                GUILayout.Label("<b>Status: <color=#FFD966>no QuadBufferManager</color></b>  " +
                                "Diagnostics only run while a QuadBufferManager is enabled in the scene.", m_entryStyle);
                GUILayout.EndVertical();
                return;
            }
            GUILayout.Label($"<b>Status: <color={StateColor(s.State)}>{s.State}</color></b>  {s.Explanation}", m_entryStyle);
            GUILayout.Label(
                $"Graphics API: {s.Renderer}   Device bound: {Mark(s.DeviceInitialized)}   " +
                $"Windowed stereo: {Mark(s.WindowedStereoEnabled)}   Full-screen stereo modes: {s.FullscreenStereoModeCount}   " +
                $"Notifications: {Mark(s.StereoStatusRegistered)} ({s.StereoStatusChangeCount} {(s.StereoStatusChangeCount == 1 ? "change" : "changes")})", m_entryStyle);
            GUILayout.Label(
                $"Unity swap chain: {SwapChainModelText(s.UnitySwapChainModel)}   " +
                $"Window: {Mark(s.HasWindow)}   Eye textures: {Mark(s.HasEyeTextures)} (valid {Mark(s.EyeTexturesValid)})   " +
                $"Stereo swap chain: {Mark(s.SwapChainCreated)} (stereo {Mark(s.SwapChainIsStereo)})   " +
                $"Frames presented: {s.FramesPresented}   Last error: {(s.LastErrorHResult == 0 ? "none" : $"0x{s.LastErrorHResult:X8}")}",
                m_entryStyle);
            if (m_environment == null)
                m_environment = QuadBufferDiagnostics.DescribeEnvironmentLines();
            foreach (var line in m_environment)
                GUILayout.Label($"<color=#AAAAAA>{Escape(line)}</color>", m_entryStyle);
            GUILayout.EndVertical();
        }

        void DrawLog()
        {
            m_scroll = GUILayout.BeginScrollView(m_scroll);
            foreach (var entry in m_entries)
            {
                if (!IsShown(entry))
                    continue;
                GUILayout.Label($"<color={TypeColor(entry.type)}>[{entry.time:HH:mm:ss.fff}] {Escape(entry.message)}</color>", m_entryStyle);
                if (m_showStackTraces && !string.IsNullOrEmpty(entry.stackTrace))
                    GUILayout.Label($"<color=#888888>{Escape(entry.stackTrace.TrimEnd())}</color>", m_entryStyle);
            }
            GUILayout.EndScrollView();
        }

        bool IsShown(Entry entry)
        {
            if (m_quadBufferOnly && !entry.message.StartsWith(QuadBufferDiagnostics.LogPrefix) &&
                !entry.message.StartsWith(QuadBufferDiagnostics.NativeLogPrefix))
                return false;
            switch (Severity(entry.type))
            {
                case LogType.Error: return m_showErrors;
                case LogType.Warning: return m_showWarnings;
                default: return m_showLog;
            }
        }

        int Count(LogType severity)
        {
            int count = 0;
            foreach (var entry in m_entries)
                if (Severity(entry.type) == severity)
                    count++;
            return count;
        }

        string BuildReport()
        {
            var s = QuadBufferDiagnostics.Status;
            var sb = new StringBuilder();
            sb.AppendLine($"QuadBuffer status: {s.State}. {s.Explanation}");
            sb.AppendLine(QuadBufferDiagnostics.DescribeEnvironment());
            sb.AppendLine();
            foreach (var entry in m_entries)
            {
                sb.AppendLine($"[{entry.time:HH:mm:ss.fff}] {entry.type}: {entry.message}");
                if (!string.IsNullOrEmpty(entry.stackTrace))
                    sb.AppendLine(entry.stackTrace.TrimEnd());
            }
            return sb.ToString();
        }

        void EnsureStyles()
        {
            if (m_entryStyle != null)
                return;
            m_entryStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 12, margin = new RectOffset(0, 0, 0, 0) };
            m_badgeStyle = new GUIStyle(m_entryStyle) { wordWrap = false };
            m_boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(6, 6, 4, 4) };
            m_background = new Texture2D(1, 1);
            m_background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
            m_background.Apply();
        }

        static LogType Severity(LogType type) =>
            type == LogType.Exception || type == LogType.Assert ? LogType.Error : type;

        static string TypeColor(LogType type)
        {
            switch (Severity(type))
            {
                case LogType.Error: return "#FF6666";
                case LogType.Warning: return "#FFD966";
                default: return "#FFFFFF";
            }
        }

        static string StateColor(QuadBufferState state)
        {
            switch (state)
            {
                case QuadBufferState.Presenting: return "#66FF66";
                case QuadBufferState.Idle:
                case QuadBufferState.Starting:
                case QuadBufferState.DeviceNotReady: return "#FFD966";
                default: return "#FF6666";
            }
        }

        static string SwapChainModelText(SwapChainModel model)
        {
            // In the Editor the model is the Editor's own and doesn't follow Player Settings.
            if (Application.isEditor && model != SwapChainModel.Unknown)
                return $"<color=#AAAAAA>{model.ToString().ToLowerInvariant()} model (Editor)</color>";
            switch (model)
            {
                case SwapChainModel.Blit: return "<color=#66FF66>blit model</color>";
                case SwapChainModel.Flip: return "<color=#FF6666>flip model</color>";
                default: return "unknown";
            }
        }

        static string Mark(bool value) => value ? "<color=#66FF66>yes</color>" : "<color=#FF6666>no</color>";

        // Rich text would otherwise interpret "<...>" inside log messages, and IMGUI draws a
        // carriage return as a jump back to the start of the line, overprinting the text.
        static string Escape(string text) => text.Replace("\r", "").Replace("<", "<" + (char)0x200B);
    }
}
