using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace LeakNinja.Tests
{
    internal class EmptyObject
    {
        internal const string ToStringValue = "EmptyObject";

        public override string ToString() => ToStringValue;
    }
    
    internal class EmptyComponent : MonoBehaviour { }
    
    internal class WatchSummaryFormatterTest
    {
        [SetUp] public void SetUp() => Time.timeScale = 100;

        // TODO test system object info
        
        [Test]
        public void TestGameObject()
        {
            var parentName = "parent";
            var parentGo = new GameObject(parentName);

            var childName = "child";
            var childGo = new GameObject(childName);
            
            var monitor = new ManualLeakNinja();
            var parent = monitor.WatchGameObject(null, parentGo);
            
            Assert.AreEqual(parentName, parent.Name);
            Assert.AreEqual(parentName, parent.FullName);
            
            var child = monitor.WatchGameObject(parent, childGo);
            
            Assert.AreEqual(childName, child.Name);
            Assert.IsTrue(child.FullName.Contains(childName));
            Assert.IsTrue(child.FullName.Contains(parentName));
        }

        [Test]
        public void TestComponent()
        {
            var gameObjName = "gameObj";
            var gameObjInstance = new GameObject(gameObjName);
            var componentInstance = gameObjInstance.AddComponent<EmptyComponent>();
            
            var monitor = new ManualLeakNinja();
            var gameObj = monitor.WatchGameObject(null, gameObjInstance);

            var component = monitor.WatchComponent(gameObj, componentInstance);
            
            Assert.IsFalse(string.IsNullOrEmpty(gameObj.FullName));
            Assert.IsTrue(component.FullName.Contains(gameObj.FullName), $"actual: {component.FullName}");
            Assert.IsTrue(component.FullName.Contains(nameof(EmptyComponent)), $"actual: {component.FullName}");
        }

        [Test]
        public void TestSummaryFormat()
        {
            GameObject go1, go2, go3, go4;
            var objects = new UnityEngine.Object[]
            {
                go1 = new GameObject("1"),
                go2 = new GameObject("2"),
                go3 = new GameObject("3"),
                go4 = new GameObject("4"),
                new Mesh(),
                go4.AddComponent<EmptyComponent>(),
                go2.AddComponent<EmptyComponent>()
            };
            
            go2.transform.SetParent(go1.transform);
            go3.transform.SetParent(go1.transform);

            var monitor = new ManualLeakNinja();
            FindAlgorithms.WatchObjects(monitor, objects);
            var systemObj = new EmptyObject();
            monitor.WatchObject(systemObj);

            var summary = Format(monitor.WatchedReferences);
            var lines = summary.Split('\n');
            var i = 0;
            Assert.AreEqual(" (UnityEngine.Mesh)", lines[i++].TrimEnd());
            Assert.AreEqual("1 (GameObject)", lines[i++].TrimEnd());
            Assert.AreEqual("  2 (GameObject EmptyComponent)", lines[i++].TrimEnd());
            Assert.AreEqual("  3 (GameObject)", lines[i++].TrimEnd());
            Assert.AreEqual("4 (GameObject EmptyComponent)", lines[i++].TrimEnd());
            Assert.IsTrue(i == lines.Length || string.IsNullOrEmpty(lines[i]));
            
            var summaryLeaked = Format(monitor.LeakedReferences);
            Assert.AreEqual(EmptyObject.ToStringValue, summaryLeaked.Trim());
        }
        
        [Test]
        public void TestSummaryTree()
        {
            var mesh = new Mesh();
            var gameObjects = new[]
            {
                new GameObject("go 0"),
                new GameObject("go 1", typeof(EmptyComponent)),
                new GameObject("go 2"),
            };
            gameObjects[1].transform.SetParent(gameObjects[0].transform);
            
            var monitor = new ManualLeakNinja();
            monitor.WatchObject(mesh);
            FindAlgorithms.WatchRecursive(monitor, gameObjects[0], null); // can use WatchObjects with one call - does not matter
            FindAlgorithms.WatchRecursive(monitor, gameObjects[2], null);

            var summary = Format(monitor.WatchedReferences, "first line");

            var lines = summary.Split('\n');
            var i = 0;
            Assert.AreEqual("first line", lines[i++].TrimEnd());
            Assert.AreEqual(" (UnityEngine.Mesh)", lines[i++].TrimEnd());
            Assert.AreEqual("go 0 (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("  go 1 (GameObject EmptyComponent Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("go 2 (GameObject Transform)", lines[i++].TrimEnd());
            Assert.IsTrue(i == lines.Length || string.IsNullOrEmpty(lines[i]));
        }
        
        [Test]
        public void TestSummaryTreeWithNonLeaking()
        {
            var mesh = new Mesh();
            var gameObjects = new[]
            {
                new GameObject("go 0"),
                new GameObject("go 1", typeof(EmptyComponent)),
                new GameObject("go 2"),
                new GameObject("go 3"),
                new GameObject("go 4"), 
            };
            gameObjects[1].transform.SetParent(gameObjects[0].transform);
            gameObjects[4].transform.SetParent(gameObjects[3].transform);
            
            var monitor = new ManualLeakNinja();
            monitor.WatchObject(mesh);
            FindAlgorithms.WatchRecursive(monitor, gameObjects[0], null);
            FindAlgorithms.WatchRecursive(monitor, gameObjects[2], null);
            // not adding go 3 to simulate it non-leaking
            FindAlgorithms.WatchRecursive(monitor, gameObjects[4], new GameWatch(null, gameObjects[3])); 

            var summary = Format(monitor.WatchedReferences, "first line");

            var lines = summary.Split('\n');
            var i = 0;
            Assert.AreEqual("first line", lines[i++].TrimEnd());
            Assert.AreEqual(" (UnityEngine.Mesh)", lines[i++].TrimEnd());
            Assert.AreEqual("go 0 (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("  go 1 (GameObject EmptyComponent Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("go 2 (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("go 3/go 4 (GameObject Transform)", lines[i++].TrimEnd());
            Assert.IsTrue(i == lines.Length || string.IsNullOrEmpty(lines[i]));
        }

        [Test]
        public void TestGroupSameNames()
        {
            var watches = GetSeveralMeshesAndGameObjects();
            
            var summary = Format(watches, "first line");
            var lines = summary.Split('\n');
            var i = 0;
            Assert.AreEqual("first line", lines[i++].TrimEnd());
            Assert.AreEqual(" (UnityEngine.Mesh) (2)", lines[i++].TrimEnd());
            Assert.AreEqual("A (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("  B (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("    C (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("    C (GameObject Transform)", lines[i++].TrimEnd());
            Assert.AreEqual("  B (GameObject Transform)", lines[i++].TrimEnd());
            Assert.IsTrue(i == lines.Length || string.IsNullOrEmpty(lines[i]));
        }

        [Test]
        public void TestSplitByLinesCount()
        {
            var watches = GetSeveralMeshesAndGameObjects();
            
            var summaries = Format(watches, 4, "first line");
            var lines = summaries.Select(s => s.Split('\n')).ToArray();
            Assert.AreEqual(2, summaries.Length);
            var i = 0;
            Assert.AreEqual("first line", lines[0][i++].TrimEnd());
            Assert.AreEqual(" (UnityEngine.Mesh) (2)", lines[0][i++].TrimEnd());
            Assert.AreEqual("A (GameObject Transform)", lines[0][i++].TrimEnd());
            Assert.AreEqual("  B (GameObject Transform)", lines[0][i++].TrimEnd());
            Assert.IsTrue(i == lines.Length || string.IsNullOrEmpty(lines[0][i]));

            i = 0;
            Assert.AreEqual("    C (GameObject Transform)", lines[1][i++].TrimEnd());
            Assert.AreEqual("    C (GameObject Transform)", lines[1][i++].TrimEnd());
            Assert.AreEqual("  B (GameObject Transform)", lines[1][i++].TrimEnd());
            Assert.IsTrue(i == lines.Length || string.IsNullOrEmpty(lines[1][i]));
        }

        private static IReadOnlyCollection<Watch> GetSeveralMeshesAndGameObjects()
        {
            var monitor = new ManualLeakNinja();
            monitor.WatchObject(new Mesh());
            monitor.WatchObject(new Mesh());

            var root = new GameObject("A")
                .AddChild(new GameObject("B")
                    .AddChild(new GameObject("C"))
                    .AddChild(new GameObject("C")))
                .AddChild(new GameObject("B"));
            
            FindAlgorithms.WatchRecursive(monitor, root, null);

            return monitor.WatchedReferences;
        }

        private static string Format(IReadOnlyCollection<Watch> watches, string firstLine = null)
        {
            var formatter = new WatchSummaryFormatter();
            formatter.Indent = "  ";
            return formatter.Format(watches, firstLine);
        }

        private static string[] Format(IReadOnlyCollection<Watch> watches, int maxLines, string firstLine = null)
        {
            var formatter = new WatchSummaryFormatter();
            formatter.Indent = "  ";
            return formatter.Format(watches, maxLines, firstLine);
        }
    }
}