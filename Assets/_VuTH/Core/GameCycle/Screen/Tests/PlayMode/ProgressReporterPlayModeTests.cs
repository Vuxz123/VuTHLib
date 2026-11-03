using System;
using System.Collections;
using System.Collections.Generic;
using _VuTH.Core.GameCycle.Screen.Loading;
using _VuTH.Core.GameCycle.Screen.Progress;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.GameCycle.Screen.Tests.PlayMode
{
    /// <summary>
    /// The loading bar must always finish: CompleteAsync used to wait for tasks to report exactly 1.0, which hung the
    /// transition forever when tasks reported fewer/more units than declared or when the game was paused.
    /// </summary>
    public class ProgressReporterPlayModeTests
    {
        private sealed class RecordingLoadingController : ILoadingController
        {
            public readonly List<float> Values = new();
            public UniTask Show() => UniTask.CompletedTask;
            public UniTask Hide() => UniTask.CompletedTask;
            public void SetProgress(float value) => Values.Add(value);
            public void SetVisible(bool isVisible) { }
        }

        private float _timeScale;

        [SetUp] public void SetUp() => _timeScale = Time.timeScale;
        [TearDown] public void TearDown() => Time.timeScale = _timeScale;

        private static IEnumerator CompletesWithin(UniTask task, float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (task.Status == UniTaskStatus.Pending)
            {
                if (Time.realtimeSinceStartup - start > seconds)
                    Assert.Fail($"CompleteAsync did not finish within {seconds}s");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CompleteAsync_FinishesAtOne_EvenWhenTasksStoppedHalfway()
        {
            var controller = new RecordingLoadingController();
            var reporter = new ProgressReporter(controller, speed: 10f);
            reporter.Report(0.5f); // e.g. a task declared units it never reported

            yield return CompletesWithin(reporter.CompleteAsync(), 3f);

            Assert.AreEqual(1f, reporter.CurrentValue);
            Assert.AreEqual(1f, controller.Values[controller.Values.Count - 1]);
        }

        [UnityTest]
        public IEnumerator Report_IsClampedAndMonotonic_SoOvershootCannotHang()
        {
            var controller = new RecordingLoadingController();
            var reporter = new ProgressReporter(controller, speed: 10f);
            reporter.Report(1.7f); // more increments than declared
            reporter.Report(0.2f); // stale value must not move the bar back

            yield return CompletesWithin(reporter.CompleteAsync(), 3f);

            Assert.AreEqual(1f, reporter.CurrentValue);
            foreach (var v in controller.Values) Assert.LessOrEqual(v, 1f);
        }

        [UnityTest]
        public IEnumerator CompleteAsync_StillFinishes_WhenGameIsPaused()
        {
            Time.timeScale = 0f;
            var reporter = new ProgressReporter(new RecordingLoadingController(), speed: 10f);

            yield return CompletesWithin(reporter.CompleteAsync(), 3f);

            Assert.AreEqual(1f, reporter.CurrentValue);
        }
    }
}
