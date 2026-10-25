using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuadBuffer.Tests
{
    public class QuadBufferCameraTests
    {
        [UnityTest]
        public IEnumerator Enable_OutsidePlayer_DoesNotCreateEyes()
        {
            LogAssert.Expect(LogType.Warning, "[QuadBuffer] Stereo output only works in a Windows standalone player.");

            var go = new GameObject("Camera", typeof(Camera), typeof(QuadBufferCamera));
            yield return null;

            Assert.IsFalse(go.GetComponent<QuadBufferCamera>().IsStereoAvailable);
            Assert.AreEqual(0, go.transform.childCount);

            Object.Destroy(go);
        }
    }
}
