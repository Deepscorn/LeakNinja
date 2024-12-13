using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeakNinja
{
    public enum MonitorAction
    {
        None,
        WatchScenes,
        WatchAll,
        Check,
        CheckImmediately
    }

    // Class to automatically watch new objects & check leaks
    public class LeakNinja : MonoBehaviour
    {
        public MonitorAction DoOnStart = MonitorAction.None;
        public MonitorAction DoOnSceneLoad = MonitorAction.WatchAll;
        public MonitorAction DoOnSceneUnload = MonitorAction.None;
        public MonitorAction DoOnEditorQuit = MonitorAction.None; // TODO find out if CheckImmediately OnEditorQuit will be useful
        public MonitorAction DoPeriodic1 = MonitorAction.Check;
        public MonitorAction DoPeriodic2 = MonitorAction.None;

        [Tooltip("Period for action in DoPeriodic1")]
        public int Period1Ms = 2000;
        [Tooltip("Period for action in DoPeriodic2")]
        public int Period2Ms = 2000;

        public bool OutputLeaks = true;

        private float nextPeriod1Time_;
        private float nextPeriod2Time_;

        public ManualLeakNinja Manual { get; private set; }

        // ReSharper disable once MemberCanBePrivate.Global (used for tests)
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public Coroutine CheckCoroutine { get; private set; }

        // ReSharper disable once UnusedMethodReturnValue.Global (can be customized through return value)
        public static LeakNinja Create()
        {
            var result = new GameObject(nameof(LeakNinja)).AddComponent<LeakNinja>();
            DontDestroyOnLoad(result.gameObject);
            return result;
        }

        private void Awake()
        {
            Manual = new ManualLeakNinja();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            Core.Log.Message("Start");
            PerformAction(DoOnStart, "Start");
            InitPeriodicPart();
        }

        // ReSharper disable once MemberCanBePrivate.Global (used from tests)
        public void PerformAction(MonitorAction action, string userMessage)
        {
            var message = $"{userMessage}({action})";
            switch (action)
            {
                case MonitorAction.WatchScenes:
                    Core.Log.Scope(message, () => FindAlgorithms.WatchAllSceneGameObjects(Manual));
                    break;
                case MonitorAction.WatchAll:
                    Core.Log.Scope(message, () => FindAlgorithms.WatchAllObjectsThatUnityHaveNow(Manual));
                    break;
                case MonitorAction.Check:
                    CheckCoroutine = StartCoroutine(PerformCheck(message));
                    break;
                case MonitorAction.CheckImmediately:
                    PerformCheckImmediately(message);
                    break;
                case MonitorAction.None:
                    break;
                default:
                    throw new NotImplementedException(action.ToString());
            }
        }

        private void FixedUpdate()
        {
            var time = Time.fixedTime;

            if (DoPeriodic1 != MonitorAction.None && time > nextPeriod1Time_)
            {
                SetNextPeriod1Time();
                PerformAction(DoPeriodic1, nameof(DoPeriodic1));
            }

            // ReSharper disable once InvertIf
            if (DoPeriodic2 != MonitorAction.None && time > nextPeriod2Time_)
            {
                SetNextPeriod2Time();
                PerformAction(DoPeriodic2, nameof(DoPeriodic2));
            }
        }

        private void OnDestroy()
        {
            Core.Log.Message("OnDestroy");
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnApplicationQuit()
        {
#if UNITY_EDITOR
            PerformAction(DoOnEditorQuit, nameof(DoOnEditorQuit));
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            => PerformAction(DoOnSceneLoad, $"{nameof(DoOnSceneLoad)}({scene.name})");

        private void OnSceneUnloaded(Scene scene)
            => PerformAction(DoOnSceneUnload, $"{nameof(DoOnSceneUnload)}({scene.name})");

        private IEnumerator PerformCheck(string logMessage)
        {
            yield return GcHelper.WaitReferencesFreed();
            var startTime = DateTime.UtcNow;
            Manual.ForceFreeUpdateLeaks();

            Core.Log.Message($"{logMessage} {(DateTime.UtcNow - startTime).TotalMilliseconds} ms");

            TryPrintLeaks();
        }

        private void PerformCheckImmediately(string logMessage)
        {
            var prevTime = DateTime.Now;
            Manual.ForceFreeUpdateLeaks();
            Core.Log.Message($"{logMessage} {(DateTime.Now - prevTime).TotalMilliseconds: 0.} ms");

            TryPrintLeaks();
        }

        private void TryPrintLeaks()
        {
            if (!OutputLeaks || Manual.LeakedReferences.Count <= 0)
                return;

            IReadOnlyCollection<string> warnings = null;
            Core.Log.Scope("Format", () =>
            {
                // splitting to several strings, because there is a limit in GameDebugConsole and unity console
                const int maxLines = 100;
                var firstLine = $"Leaks({Manual.LeakedReferences.Count}):";
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                warnings = new WatchSummaryFormatter().Format(Manual.LeakedReferences, maxLines, firstLine);
            });
            foreach (var warning in warnings)
                Core.Log.Warning(warning);
        }

        private void InitPeriodicPart()
        {
            if (DoPeriodic1 != MonitorAction.None)
                SetNextPeriod1Time();

            if (DoPeriodic2 != MonitorAction.None)
            {
                SetNextPeriod2Time();
                if (!(nextPeriod1Time_ >= 0))
                    return;
                // add delay between actions to minimize freezes
                var delay = Mathf.Min(Period1Ms * 0.0005f, Period2Ms * 0.0005f);
                if (Mathf.Abs(nextPeriod1Time_ - nextPeriod2Time_) < delay)
                    nextPeriod2Time_ += delay;
            }
        }

        private void SetNextPeriod1Time() => nextPeriod1Time_ = Time.fixedTime + Period1Ms * 0.001f;

        private void SetNextPeriod2Time() => nextPeriod2Time_ = Time.fixedTime + Period2Ms * 0.001f;
    }

    // LeakNinja further development:
    // TODO make cheat that will dump leaked refs to slack like "sl" does,
    // TODO make cheat that will clean up (long-running, try optimize to 10 minutes), so that only roots are left, all dependents removed.
    // This dump also upload to slack (optional - try implementing it on device, so not need separate pc for dumping)
    // TODO Add IsNeverBeenActive flag. It's common error to free something in OnDestroy. For components that never was active
    // no lifecycle events are called. So, if you initialize something manually, you'll get a leak
    // TODO check yield return WaitFixedUpdate works on device (will speed up timescaled tests)
}