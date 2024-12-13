using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace LeakNinja.Tests
{
    internal class ManualLeakNinjaTest
    {
        private class EmptyComponent : MonoBehaviour {}
        
        [SetUp] public void SetUp() => Time.timeScale = 100;

        // test object must die
        [UnityTest]
        public IEnumerator TestMustDie()
        {
            var obj = new object();
            
            var monitor = new ManualLeakNinja();
            monitor.WatchObject(obj);
            
            yield return LeakTestUtils.CheckLeakNinja(monitor, 1, 1);

            // ReSharper disable once RedundantAssignment
            // Remove strong reference
            obj = null;
            yield return LeakTestUtils.CheckLeakNinja(monitor, 0);
        }

        [UnityTest] public IEnumerator TestMeshLeak() => LeakTestUtils.TestUnityObjectLeak(new Mesh());
        
        [UnityTest] public IEnumerator TestComponentLeak() => LeakTestUtils.TestUnityObjectLeak(new GameObject().AddComponent<EmptyComponent>());

        [UnityTest]
        public IEnumerator TestMultipleLeaks()
        {
            var objects = new[] { new object() };

            var unityObjects = new UnityEngine.Object[]
            {
                new GameObject(),
                new Mesh(),
                new GameObject().AddComponent<EmptyComponent>()
            };
            
            var expectedLeakedCount = 0;
            var expectedWatchedCount = 0;
            
            var monitor = new ManualLeakNinja();
            
            foreach (var obj in objects)
            {
                expectedLeakedCount++;
                expectedWatchedCount++;
                monitor.WatchObject(obj);
                yield return LeakTestUtils.CheckLeakNinja(monitor, expectedWatchedCount, expectedLeakedCount);
            }

            foreach (var obj in unityObjects)
            {
                expectedWatchedCount++;
                monitor.WatchObject(obj);
                yield return LeakTestUtils.CheckLeakNinja(monitor, expectedWatchedCount, expectedLeakedCount);
            }
            
            foreach (var obj in unityObjects)
            {
                expectedLeakedCount++;
                yield return LeakTestUtils.DestroyAndWait(obj);
                yield return LeakTestUtils.CheckLeakNinja(monitor, expectedWatchedCount, expectedLeakedCount);
            }

            yield return CheckLeaksFreed(unityObjects, monitor);
            yield return CheckLeaksFreed(objects, monitor);
        }

        private static IEnumerator CheckLeaksFreed<T>(T[] strongReferences, ManualLeakNinja monitor) where T : class
        {
            int expectedWatchedCount = monitor.TotalWatchedReferencesCount;
            int expectedLeakedCount = monitor.LeakedReferences.Count;
            
            for (int i = 0; i < strongReferences.Length; i++)
            {
                expectedLeakedCount--;
                expectedWatchedCount--;
                // Remove strong reference
                strongReferences[i] = null;
                yield return LeakTestUtils.CheckLeakNinja(monitor, expectedWatchedCount, expectedLeakedCount);
            }
        }
    }
}