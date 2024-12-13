// #define ENABLE_LOGS
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace LeakNinja.Tests
{
    internal static class LeakTestUtils
    {
        internal static IEnumerator TestUnityObjectLeak(UnityEngine.Object obj)
        {
            var monitor = new ManualLeakNinja();
            monitor.WatchObject(obj);
            
            yield return CheckLeakNinja(monitor, 1, 0);
            
            yield return DestroyAndWait(obj);
            
            yield return CheckLeakNinja(monitor, 1, 1);
        
            // ReSharper disable once RedundantAssignment
            // Remove strong reference
            obj = null;
            yield return CheckLeakNinja(monitor, 0);
        }
        
        internal static IEnumerator CheckLeakNinja(ManualLeakNinja monitor, int expectedTotalReferencesCount, 
            int expectedLeakingReferencesCount = 0)
        {
            if (expectedLeakingReferencesCount > expectedTotalReferencesCount)
                throw new ArgumentException();

            yield return GcHelper.WaitReferencesFreed();
            monitor.ForceFreeUpdateLeaks();

            LogWatchSummary(monitor);
            
            Assert.AreEqual(expectedLeakingReferencesCount, monitor.LeakedReferences.Count);
            Assert.AreEqual(expectedTotalReferencesCount, monitor.TotalWatchedReferencesCount);
        }

        internal static IEnumerator DestroyAndWait(UnityEngine.Object obj)
        {
            UnityEngine.Object.Destroy(obj);

            yield return WaitUtils.WaitUntil(() => obj == null);
        }

        [Conditional("ENABLE_LOGS")]
        private static void LogWatchSummary(ManualLeakNinja monitor)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"total: {monitor.TotalWatchedReferencesCount} leaked: {monitor.LeakedReferences.Count}");
            foreach (var leak in monitor.LeakedReferences)
            {
                builder.AppendLine(leak.ToString());
            }
            Debug.Log(builder.ToString());
        }
    }
}