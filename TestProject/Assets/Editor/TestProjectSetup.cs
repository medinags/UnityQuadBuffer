using System.IO;
using QuadBuffer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One-click setup and build of the quad-buffer test player.</summary>
public static class TestProjectSetup
{
    const string ScenePath = "Assets/Scenes/StereoTest.unity";
    const string BuildPath = "Builds/Windows/QuadBufferTest.exe";

    [MenuItem("QuadBuffer/Configure Project")]
    public static void Configure()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });

        // DXGI allows only one flip-model swap chain per window. With Unity on the older blit
        // model, the plugin can add its stereo (flip-model) swap chain to Unity's window.
        PlayerSettings.useFlipModelSwapchain = false;

        if (!File.Exists(ScenePath))
            CreateTestScene();
        else
            UpdateTestScene();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[QuadBuffer] Test project configured: Direct3D11 only, DXGI flip model swap chain off.");
    }

    [MenuItem("QuadBuffer/Build Windows Player")]
    public static void Build()
    {
        Configure();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            // Development build: includes the QuadBuffer console overlay (F12).
            options = BuildOptions.Development,
        });

        var summary = report.summary;
        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[QuadBuffer] Player built: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB).");
        else
            Debug.LogError($"[QuadBuffer] Player build {summary.result} with {summary.totalErrors} error(s).");
    }

    static void CreateTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera.main.transform.position = new Vector3(0f, 1f, -5f);
        // The manager adds the QuadBufferCamera to the main camera when the scene starts.
        AddManager(Camera.main);

        // A few objects at different depths so the stereo effect is visible.
        for (int i = 0; i < 5; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(i - 2f, 1f, i * 1.5f);
            cube.AddComponent<StereoTestAnimator>();
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    // Brings older test scenes up to date: adds the QuadBufferManager and the cube animation.
    static void UpdateTestScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        bool changed = false;

        var manager = Object.FindAnyObjectByType<QuadBufferManager>();
        if (manager == null)
        {
            AddManager(Camera.main);
            Debug.Log($"[QuadBuffer] Added a QuadBufferManager to {ScenePath}.");
            changed = true;
        }
        else if (manager.Console == QuadBufferManager.ConsoleMode.Auto)
        {
            SetConsoleAlways(manager);
            Debug.Log($"[QuadBuffer] Set the QuadBufferManager console to Always in {ScenePath}, so release builds have it too.");
            changed = true;
        }

        int animated = 0;
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>())
        {
            if (renderer.GetComponent<StereoTestAnimator>() == null)
            {
                renderer.gameObject.AddComponent<StereoTestAnimator>();
                animated++;
            }
        }
        if (animated > 0)
        {
            Debug.Log($"[QuadBuffer] Added StereoTestAnimator to {animated} object(s) in {ScenePath}.");
            changed = true;
        }

        if (changed)
            EditorSceneManager.SaveScene(scene);
    }

    static void AddManager(Camera stereoCamera)
    {
        var manager = new GameObject("QuadBuffer Manager").AddComponent<QuadBufferManager>();
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("targetCamera").objectReferenceValue = stereoCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetConsoleAlways(manager);
    }

    // Test players run on stereo machines where the console is the quickest way to see the status.
    static void SetConsoleAlways(QuadBufferManager manager)
    {
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("console").enumValueIndex = (int)QuadBufferManager.ConsoleMode.Always;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
