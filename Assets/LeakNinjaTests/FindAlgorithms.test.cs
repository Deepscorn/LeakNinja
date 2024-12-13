using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LeakNinja.Tests
{
    internal static class GameWatchExtensions
    {
        public static Watch Parent(this Watch watch) => ((GameWatch) watch).Parent;
    }
    
    internal class FindAlgorithmsTest
    {
        [SetUp] public void SetUp() => Time.timeScale = 100;
        
        [Test]
        public void TestWatchRecursive()
        {
            var root = CreateGameObjectTree();

            var monitor = new ManualLeakNinja();
            FindAlgorithms.WatchRecursive(monitor, root, null);
            
            Assert.AreEqual(12, monitor.TotalWatchedReferencesCount); // 6 game objects + 6 transforms
        }

        [Test]
        public void TestWatchAllObjectsThatUnityHaveNow()
        {
            var root = CreateGameObjectTree();
            var monitor = new ManualLeakNinja();
            FindAlgorithms.WatchAllObjectsThatUnityHaveNow(monitor);

            AssertContainsRecursive(monitor, root);
        }

        [Test]
        public void TestWatchAllSceneGameObjects()
        {
            var root = CreateGameObjectTree();
            var monitor = new ManualLeakNinja();
            FindAlgorithms.WatchAllSceneGameObjects(monitor);

            AssertContainsRecursive(monitor, root);
        }
        
        [Test]
        public void TestWatchRecursiveNotAddSameObjectTwice()
        {
            var root = CreateGameObjectTree();

            var monitor = new ManualLeakNinja();
            FindAlgorithms.WatchRecursive(monitor, root, null);
            FindAlgorithms.WatchRecursive(monitor, root, null);
            
            Assert.AreEqual(12, monitor.TotalWatchedReferencesCount); // 6 game objects + 6 transforms
        }
        
        // test watches leaked game object which is destroyed by scene unload
        [UnityTest]
        public IEnumerator TestWatchUnloadedSceneGameObjectLeak()
            => CheckWatchUnloadedSceneLeak(() => new GameObject("Obj"));
        
        // test watches leaked transform which is destroyed by scene unload
        [UnityTest]
        public IEnumerator TestWatchUnloadedSceneTransformLeak()
            => CheckWatchUnloadedSceneLeak(() => new GameObject("Obj").transform);

        [Test]
        public void TestWatchObjects()
        {
            GameObject child, gameObj;
            var root = new GameObject("root")
                .AddChild(gameObj = new GameObject("gameObj")
                    .AddChild(child = new GameObject("child")));
            
            var monitor = new ManualLeakNinja();
            FindAlgorithms.WatchObjects(monitor, new UnityEngine.Object[] { root, child, gameObj });
            
            var childInfo = (GameWatch) monitor.GetWatchInfo(child);
            Assert.AreSame(child, childInfo.Object);
            Assert.AreSame(gameObj, childInfo.Parent().Object);
            Assert.AreSame(root, childInfo.Parent().Parent().Object);
            Assert.IsNull(childInfo.Parent().Parent().Parent());
        }

        private IEnumerator CheckWatchUnloadedSceneLeak(Func<UnityEngine.Object> factory)
        {
            var monitor = new ManualLeakNinja();
            
            var scene = SceneManager.CreateScene("TempScene");
            SceneManager.SetActiveScene(scene);
            var obj = factory();
            
            FindAlgorithms.WatchSceneGameObjects(monitor, scene);

            yield return LeakTestUtils.CheckLeakNinja(monitor, 2, 0);

            yield return SceneManager.UnloadSceneAsync(scene);

            yield return LeakTestUtils.CheckLeakNinja(monitor, 1, 1);

            obj = null;
            yield return LeakTestUtils.CheckLeakNinja(monitor, 0);
        }

        private void AssertContainsRecursive(ManualLeakNinja monitor, GameObject gameObject)
        {
            Assert.IsTrue(monitor.WatchedReferences.Any(o => ReferenceEquals(o.Object, gameObject)), $"{gameObject.name}");
            Assert.IsTrue(monitor.WatchedReferences.Any(o => ReferenceEquals(o.Object, gameObject.transform)), $"{gameObject.name} (Transform)");
            foreach (Transform child in gameObject.transform)
                AssertContainsRecursive(monitor, child.gameObject);
        }

        private GameObject CreateGameObjectTree() => 
            new GameObject("root")
                .AddChild(new GameObject("subroot")
                    .AddChildren(new GameObject("leaf1"), new GameObject("leaf2")))
                .AddChild(new GameObject("leaf3"))
                .AddChild(new GameObject("leaf4"));
    }
}