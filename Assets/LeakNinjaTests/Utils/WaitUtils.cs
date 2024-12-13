using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeakNinja.Tests
{
    // Tests are speed up by timeScale increase. But updates are not (frame dependent).
    // And WaitForSeconds, WaitWhile, yield return null - all are called as frequent, as update.
    // So need to use other variants which are called more frequently with speed up. Here they are in this class
    internal static class WaitUtils
    {
        internal static readonly WaitForFixedUpdate WaitForFixedUpdate = new WaitForFixedUpdate();

        internal static IEnumerator WaitForSeconds(float seconds)
        {
            var prevTime = Time.time;
            while (Time.time < prevTime + seconds)
                yield return WaitForFixedUpdate;

            var actualDelay = Time.time - prevTime;
            Assert.AreEqual(seconds, actualDelay, 0.05f, "WaitSeconds");
        }

        internal static IEnumerator WaitUntil(Func<bool> expr)
        {
            while (!expr())
                yield return WaitForFixedUpdate;
        }
    }
    
    internal class TestUtilsTest
    {
        [UnityTest]
        public IEnumerator TestWait()
        {
            Time.timeScale = 100f;
            yield return WaitUtils.WaitForSeconds(0.1f);
            yield return WaitUtils.WaitForSeconds(0.2f);
            yield return WaitUtils.WaitForSeconds(1f);
            
            Time.timeScale = 10f;
            yield return WaitUtils.WaitForSeconds(0.1f);
            yield return WaitUtils.WaitForSeconds(0.2f);
            yield return WaitUtils.WaitForSeconds(1f);
        }
    }
}