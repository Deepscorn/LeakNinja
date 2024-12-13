using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LeakNinja.Tests
{
    internal class LeakNinjaTest
    {
        private const string TestScenePath = "LeakNinjaTests/SampleScene";

        [SetUp]
        public void SetUp() => Time.timeScale = 100f;
        
        [UnityTest]
        public IEnumerator TestForceWatchAndCheck()
        {
            var monitor = CreateLeakNinja();
            
            var scene = SceneManager.CreateScene("TempScene");
            SceneManager.SetActiveScene(scene);
            var obj = new GameObject();
            
            monitor.PerformAction(MonitorAction.WatchScenes, "ForceWatch");

            yield return CheckLeakNinjaMin(monitor, 2, 0);

            yield return SceneManager.UnloadSceneAsync(scene);

            yield return CheckLeakNinjaMin(monitor, 1, 1);

            obj = null;
            yield return CheckLeakNinjaMin(monitor, 0, 0);
        }

        [Test]
        public void TestWatchAlgorithms()
        {
            TestWatchAlgorithm(new GameObject(), true, MonitorAction.WatchScenes);
            
            var mesh = new Mesh();
            TestWatchAlgorithm(mesh, false, MonitorAction.WatchScenes);
            TestWatchAlgorithm(mesh, true, MonitorAction.WatchAll);
        }

        [UnityTest]
        public IEnumerator TestWatchMoments()
        {
            // TODO add more moments
            var obj = new GameObject();
            foreach (TestMoment moment in new [] 
                { TestMoment.None, TestMoment.OnStart, TestMoment.Periodic1, TestMoment.OnSceneLoad})
            {
                Core.Log.Message($">>>> test {moment}");
                var monitor = CreateLeakNinja();
                yield return TestWatchMoment(monitor, obj, moment);
                UnityEngine.Object.Destroy(monitor);
                // yield return new WaitUntil(() => monitor == null);
                Core.Log.Message($"<<<< test {moment}");
            }
            UnityEngine.Object.Destroy(obj);
        }
        
        [UnityTest]
        public IEnumerator TestCheckMoments()
        {
            // TODO add more moments
            foreach (TestMoment mode in new [] { TestMoment.None, TestMoment.Periodic1, TestMoment.OnSceneUnload })
            {
                Core.Log.Message($">>>> test {mode}");
                var monitor = CreateLeakNinja();
                yield return TestCheckMoment(monitor, mode);
                UnityEngine.Object.Destroy(monitor);
                // yield return new WaitUntil(() => monitor == null);
                Core.Log.Message($"<<<< test {mode}");
            }
        }
        
        private IEnumerator TestWatchMoment(LeakNinja monitor, object obj, TestMoment moment)
        {
            SetTheOnlyAction(monitor, moment, MonitorAction.WatchScenes);
            monitor.Period1Ms = 300;
            
            Assert.AreEqual(0, monitor.Manual.TotalWatchedReferencesCount);

            Core.Log.Message(">>>> WaitForFixedUpdate");
            yield return WaitUtils.WaitForFixedUpdate; // wait for start to be executed
            Core.Log.Message("<<<< WaitForFixedUpdate");

            TestExist(monitor, moment == TestMoment.OnStart, obj);
            if (moment == TestMoment.OnStart)
                yield break;

            var delay = monitor.Period1Ms * 0.001f;
            Core.Log.Message($">>>> WaitFixedSeconds({delay})");
            yield return WaitUtils.WaitForSeconds(delay);
            Core.Log.Message($"<<<< WaitFixedSeconds({delay})");
            
            TestExist(monitor, moment == TestMoment.Periodic1, obj);
            if (moment == TestMoment.Periodic1)
                yield break;

            yield return SceneManager.LoadSceneAsync(TestScenePath, LoadSceneMode.Additive);
            
            TestExist(monitor, moment == TestMoment.OnSceneLoad, obj);
            if (moment == TestMoment.OnSceneLoad)
                yield break;
            
            Assert.AreEqual(TestMoment.None, moment, $"No test for moment: {moment}");
        }
        
        private IEnumerator TestCheckMoment(LeakNinja monitor, TestMoment moment)
        {
            SetTheOnlyAction(monitor, moment, MonitorAction.Check);
            var obj = new GameObject();
            monitor.Manual.WatchObject(obj);
            UnityEngine.Object.Destroy(obj);
            monitor.Period1Ms = 300;
            
            Assert.AreEqual(0, monitor.Manual.LeakedReferences.Count);

            yield return WaitUtils.WaitForFixedUpdate; // wait for start to be executed
            TestExist(monitor, true, obj);
            TestLeakExist(monitor, false, obj);

            var delay = monitor.Period1Ms * 0.001f;
            var prevCoroutine = monitor.CheckCoroutine;
            yield return WaitUtils.WaitForSeconds(delay + 0.1f);
            
            if (moment == TestMoment.Periodic1)
            {
                Assert.AreNotSame(prevCoroutine, monitor.CheckCoroutine);
                yield return monitor.CheckCoroutine;
            }
            
            TestLeakExist(monitor, moment == TestMoment.Periodic1, obj);
            if (moment == TestMoment.Periodic1)
                yield break;

            var load = SceneManager.LoadSceneAsync(TestScenePath, LoadSceneMode.Additive);
            Scene scene = default;
            SceneManager.sceneLoaded += (s, _) => scene = s; 
            yield return load;
            prevCoroutine = monitor.CheckCoroutine;
            yield return SceneManager.UnloadSceneAsync(scene);
            
            if (moment == TestMoment.OnSceneUnload)
            {
                Assert.AreNotSame(prevCoroutine, monitor.CheckCoroutine);
                yield return monitor.CheckCoroutine;
            }
            
            TestLeakExist(monitor, moment == TestMoment.OnSceneUnload, obj);
            if (moment == TestMoment.OnSceneUnload)
                yield break;
            
            Assert.IsTrue(moment == TestMoment.None, $"No test for moment: {moment}");
        }

        private void TestWatchAlgorithm(object obj, bool found, MonitorAction watchAlgorithm)
        {
            var monitor = CreateLeakNinja();
            
            Assert.AreEqual(0, monitor.Manual.TotalWatchedReferencesCount);
            monitor.PerformAction(watchAlgorithm, "ForceWatch");

            TestExist(monitor, found, obj);
        }

        private void TestLeakExist(LeakNinja monitor, bool exist, object obj)
            => Assert.AreEqual(exist, monitor.Manual.LeakedReferences
                .Any(r => r.HasObject && r.Object == obj));

        private void TestExist(LeakNinja monitor, bool exist, object obj)
            => Assert.AreEqual(exist, monitor.Manual.WatchedReferences
                .Any(r => r.HasObject && r.Object == obj));
        
        private static IEnumerator CheckLeakNinjaMin(LeakNinja monitor, int minExpectedTotalReferencesCount, 
            int expectedLeakingReferencesCount)
        {
            if (expectedLeakingReferencesCount > minExpectedTotalReferencesCount)
                throw new ArgumentException();
            
            monitor.PerformAction(MonitorAction.Check, "ForceCheck");
            yield return monitor.CheckCoroutine;
            
            Assert.AreEqual(expectedLeakingReferencesCount, monitor.Manual.LeakedReferences.Count);
            Assert.LessOrEqual(minExpectedTotalReferencesCount, monitor.Manual.TotalWatchedReferencesCount);
        }

        private LeakNinja CreateLeakNinja()
        {
            var result = LeakNinja.Create();
            result.OutputLeaks = false;
            return result;
        }

        private enum TestMoment
        {
            None,
            OnStart,
            OnSceneLoad,
            OnSceneUnload,
            OnEditorQuit,
            Periodic1,
            Periodic2
        }

        private static void SetTheOnlyAction(LeakNinja monitor, TestMoment setMoment, MonitorAction action)
        {
            foreach (TestMoment moment in Enum.GetValues(typeof(TestMoment)))
                SetActionToMoment(monitor, moment, moment == setMoment ? action : MonitorAction.None);
        }

        private static void SetActionToMoment(LeakNinja monitor, TestMoment moment, MonitorAction action)
        {
            switch (moment)
            {
                case TestMoment.OnStart:
                    monitor.DoOnStart = action;
                    return;
                case TestMoment.OnSceneLoad:
                    monitor.DoOnSceneLoad = action;
                    return;
                case TestMoment.OnSceneUnload:
                    monitor.DoOnSceneUnload = action;
                    return;
                case TestMoment.OnEditorQuit:
                    monitor.DoOnEditorQuit = action;
                    return;
                case TestMoment.Periodic1:
                    monitor.DoPeriodic1 = action;
                    return;
                case TestMoment.Periodic2:
                    monitor.DoPeriodic2 = action;
                    return;
                case TestMoment.None:
                    return;
                default:
                    throw new NotImplementedException(moment.ToString());
            }
        }
    }
}