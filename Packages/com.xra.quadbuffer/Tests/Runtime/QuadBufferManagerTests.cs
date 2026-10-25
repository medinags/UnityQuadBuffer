using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuadBuffer.Tests
{
    public class QuadBufferManagerTests
    {
        static Camera CreateCamera(string name, string tag = "Untagged")
        {
            var go = new GameObject(name, typeof(Camera)) { tag = tag };
            return go.GetComponent<Camera>();
        }

        static IEnumerator DestroyAll(params Object[] objects)
        {
            foreach (var o in objects)
                if (o != null)
                    Object.Destroy(o is Component c ? c.gameObject : o);
            yield return null; // Let OnDisable unregister the manager before the next test.
        }

        [UnityTest]
        public IEnumerator SetCamera_AddsQuadBufferCamera()
        {
            var cam = CreateCamera("Stereo Camera");
            var manager = new GameObject("Manager").AddComponent<QuadBufferManager>();

            manager.SetCamera(cam);
            Assert.IsNotNull(cam.GetComponent<QuadBufferCamera>());
            Assert.AreSame(cam, manager.TargetCamera);

            yield return null; // Start must keep the camera set by script.
            Assert.AreSame(cam.GetComponent<QuadBufferCamera>(), manager.StereoCamera);

            yield return DestroyAll(manager, cam);
        }

        [UnityTest]
        public IEnumerator Start_WithoutAssignedCamera_FindsMainCamera()
        {
            var cam = CreateCamera("Main", "MainCamera");
            var manager = new GameObject("Manager").AddComponent<QuadBufferManager>();

            yield return null; // Start searches for a camera.
            Assert.AreSame(cam, manager.TargetCamera);
            Assert.IsNotNull(cam.GetComponent<QuadBufferCamera>());

            yield return DestroyAll(manager, cam);
        }

        [UnityTest]
        public IEnumerator SetCamera_ToAnotherCamera_RemovesAddedComponent()
        {
            var first = CreateCamera("First");
            var second = CreateCamera("Second");
            var manager = new GameObject("Manager").AddComponent<QuadBufferManager>();

            manager.SetCamera(first);
            manager.SetCamera(second);
            Assert.IsFalse(first.GetComponent<QuadBufferCamera>().enabled, "Old camera must stop immediately.");

            yield return null; // Destroy completes at the end of the frame.
            Assert.IsNull(first.GetComponent<QuadBufferCamera>());
            Assert.IsNotNull(second.GetComponent<QuadBufferCamera>());

            yield return DestroyAll(manager, first, second);
        }

        [UnityTest]
        public IEnumerator SetCamera_Null_LogsErrorAndKeepsCamera()
        {
            var cam = CreateCamera("Stereo Camera");
            var manager = new GameObject("Manager").AddComponent<QuadBufferManager>();
            manager.SetCamera(cam);

            LogAssert.Expect(LogType.Error, new Regex(@"SetCamera\(null\) ignored"));
            manager.SetCamera(null);
            Assert.AreSame(cam, manager.TargetCamera);

            yield return DestroyAll(manager, cam);
        }

        [UnityTest]
        public IEnumerator Diagnostics_RunOnlyWhileManagerIsEnabled()
        {
            Assert.IsFalse(QuadBufferDiagnostics.IsRunning, "No manager yet.");

            var manager = new GameObject("Manager").AddComponent<QuadBufferManager>();
            Assert.IsTrue(QuadBufferDiagnostics.IsRunning);
            Assert.AreSame(manager, QuadBufferDiagnostics.Manager);

            manager.enabled = false;
            Assert.IsFalse(QuadBufferDiagnostics.IsRunning);

            yield return DestroyAll(manager);
        }
    }
}
