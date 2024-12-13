using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using NUnit.Framework;

namespace LeakNinja.Tests
{
    internal class CoroutineTrackedRunner : MonoBehaviour
    {
        private const float WaitTimeout = 5f;
        private int runningCount_;
        public int FailedCount;

        public static CoroutineTrackedRunner Create() => new GameObject(nameof(CoroutineTrackedRunner))
            .AddComponent<CoroutineTrackedRunner>();
        
        public void StartCoroutineTracked(IEnumerator enumerator) => StartCoroutine(WrapTracking(enumerator));

        private IEnumerator WrapTracking(IEnumerator enumerator)
        {
            runningCount_++;
            Exception exception = null;
            yield return CoroutineCatchUtils.TryCatchEnumerate(enumerator, ex => exception = ex);
            runningCount_--;
            if (exception == null) 
                yield break;
            FailedCount++;
            throw new Exception("", exception); // throw so it is logged the system way
        }

        public IEnumerator WaitTrackedCoroutinesFinished()
        {
            var time = DateTime.Now;
            while (runningCount_ > 0)
            {
                Assert.Less((DateTime.Now - time).TotalSeconds, WaitTimeout, $"Wait coroutines({runningCount_}) finish timed out");
                yield return WaitUtils.WaitForFixedUpdate;
            }
        }
    }

    internal static class CoroutineCatchUtils
    {
        // Enumerates till first exception occurs, if occured => calls onException
        // It does not throw exception. So it is up to client code to decide how to handle it. In other words:
        // yes, client code routine can continue if want to
        internal static IEnumerator TryCatchEnumerate(IEnumerator enumerator, Action<Exception> onCatched)
        {
            Exception childException = null;
            while (childException == null)
            {
                try
                {
                    if (!enumerator.MoveNext())
                        break;
                }
                catch (Exception exception)
                {
                    onCatched(exception);
                    break;
                }

                if (enumerator.Current is IEnumerator current)
                {
                    yield return TryCatchEnumerate(current, e => childException = e);
                    if (childException != null)
                        onCatched(childException);
                }
                else
                    yield return enumerator.Current;
            }
        }

        // Fixes http://fogbugz.unity3d.com/default.asp?1295307_i58749pgv5aam4b6
        // (Test not stop on first assertion in specific case and assertion message may be lost (like silent crash))
        // usage: [UnityTest] IEnumerator TestSome() => TestEnumerate(ConcreteTestRoutine());
        internal static IEnumerator TestEnumerate(IEnumerator enumerator) =>
            // rethrowing as new Exception to not lose original stacktrace
            TryCatchEnumerate(enumerator, ex => throw new Exception("", ex));
    }
    
    internal class ConceptTest
    {
        [SetUp]
        public void SetUp() => Time.timeScale = 100;

        // TODO add test variants with different timescales
        // TODO add test under CI (I assume, editor CI batch will have different behaviour)
        [UnityTest]
        public IEnumerator TestIdeaFloatingError() => CoroutineCatchUtils.TestEnumerate(TestIdeaFloatingErrorRoutine());

        private IEnumerator TestIdeaFloatingErrorRoutine()
        {
            // run several iterations to increase a chance of reproducing floating error
            // it runs tests in parallel to speed up
            // Unity, by default, requires any logging (e.g. error/exception) occured during test to be expected explicitly
            // Disabling it, because by default, current impl logs errors that way
            LogAssert.ignoreFailingMessages = true;
            var iRuns = 5; // TODO use 55, because 5 not find errors sometimes (repro: implementation without timescale multiplication)
            var jRuns = 2;
            int failedCount;
            var coroutineHelper = CoroutineTrackedRunner.Create();
            for (int i = 0; i < iRuns; i++)
            {
                for (int j = 0; j < jRuns; j++)
                    coroutineHelper.StartCoroutineTracked(TestIdea());
                yield return coroutineHelper.WaitTrackedCoroutinesFinished();
            }
            failedCount = coroutineHelper.FailedCount;
            var iterations = iRuns * jRuns;
            Assert.AreEqual(0, failedCount, $"error rate: {failedCount} / {iterations} => {(int)(failedCount / (float) iterations * 100)} %");
        }

        [UnityTest]
        public IEnumerator TestIdea()
        {
            var obj = new GameObject();
            
            var wref = new WeakReference<UnityEngine.Object>(obj);

            yield return CheckWeakReference(wref, true, false);
            
            yield return LeakTestUtils.DestroyAndWait(obj);
            
            yield return CheckWeakReference(wref, true, true);

            // ReSharper disable once RedundantAssignment
            // Remove strong reference
            obj = null;
            yield return CheckWeakReference(wref, false);
        }

        [UnityTest]
        public IEnumerator TestIdeaImpl() => LeakTestUtils.TestUnityObjectLeak(new GameObject());

        private IEnumerator CheckWeakReference<T>(WeakReference<T> wref, bool hasTarget, bool targetIsDestroyed = false) 
            where T : UnityEngine.Object
        {
            // free here so we allways get same check results despite of when gc is happened
            yield return GcHelper.WaitReferencesFreed();
            GC.Collect();

            Assert.AreEqual(hasTarget, wref.TryGetTarget(out var obj));
            if (!hasTarget)
                yield break;
            Assert.AreEqual(targetIsDestroyed, obj == null);
        }
    }
}