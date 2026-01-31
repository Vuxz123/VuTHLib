using System;
using System.Collections;
using UnityEngine;

namespace _VuTH.Common
{
    public static class MonoExtension
    {
        /// <summary>
        /// Start a coroutine only if the MonoBehaviour is active and enabled. Returns null if it cannot start.
        /// </summary>
        public static Coroutine StartCoroutineSafe(this MonoBehaviour mono, IEnumerator routine)
        {
            if (!mono || routine == null) return null;
            return !mono.isActiveAndEnabled ? null : mono.StartCoroutine(routine);
        }

        /// <summary>
        /// Stop a coroutine if both owner and routine are valid.
        /// </summary>
        public static void StopCoroutineSafe(this MonoBehaviour mono, Coroutine routine)
        {
            if (!mono || routine == null) return;
            mono.StopCoroutine(routine);
        }

        /// <summary>
        /// Run an action after a delay (game time by default). Returns the Coroutine so you can stop it.
        /// </summary>
        public static Coroutine RunAfter(this MonoBehaviour mono, float seconds, Action action, bool realtime = false)
        {
            return !mono ? null : mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                if (realtime)
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
                else
                    yield return new WaitForSeconds(Mathf.Max(0f, seconds));
                action?.Invoke();
            }
        }

        /// <summary>
        /// Run an action on the next frame.
        /// </summary>
        public static Coroutine RunNextFrame(this MonoBehaviour mono, Action action)
        {
            return !mono ? null : mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                yield return null;
                action?.Invoke();
            }
        }

        /// <summary>
        /// Run an action at the end of the current frame (after rendering).
        /// </summary>
        public static Coroutine RunAtEndOfFrame(this MonoBehaviour mono, Action action)
        {
            return !mono ? null : mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                yield return new WaitForEndOfFrame();
                action?.Invoke();
            }
        }

        /// <summary>
        /// Repeatedly run an action every interval seconds until a stop condition is met or the MonoBehaviour is disabled/destroyed.
        /// If 'until' is null, it will run indefinitely (or until disabled). Returns the Coroutine.
        /// </summary>
        public static Coroutine RunEvery(this MonoBehaviour mono, float interval, Action tick, Func<bool> until = null, bool realtime = false)
        {
            if (!mono) return null;
            interval = Mathf.Max(0f, interval);
            return mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                var wait = realtime ? (object)new WaitForSecondsRealtime(interval) : new WaitForSeconds(interval);
                while (mono && mono.isActiveAndEnabled)
                {
                    tick?.Invoke();
                    if (until != null && until())
                        yield break;
                    if (interval > 0f)
                        yield return wait;
                    else
                        yield return null; // avoid tight loop when interval is 0
                }
            }
        }

        /// <summary>
        /// Variant: invoke 'action' once when 'condition' becomes true.
        /// If condition is already true at call time, 'action' is invoked immediately without starting a coroutine.
        /// </summary>
        public static Coroutine RunWhen(this MonoBehaviour mono, Func<bool> condition, Action action)
        {
            if (condition == null) return null;

            // If already satisfied, invoke immediately regardless of owner state
            if (condition())
            {
                action?.Invoke();
                return null;
            }

            // If not satisfied yet and no owner to run a coroutine on, we cannot wait
            if (!mono) return null;

            return mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                yield return new WaitUntil(condition);
                action?.Invoke();
            }
        }

        /// <summary>
        /// Run tick every frame (or every interval seconds) while 'condition' returns true.
        /// Stops when condition becomes false or the MonoBehaviour is disabled/destroyed.
        /// </summary>
        public static Coroutine RunWhile(this MonoBehaviour mono, Func<bool> condition, Action tick, float intervalSeconds = 0f, bool realtime = false)
        {
            if (!mono || condition == null) return null;
            intervalSeconds = Mathf.Max(0f, intervalSeconds);
            return mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                var wait = realtime ? (object)new WaitForSecondsRealtime(intervalSeconds) : new WaitForSeconds(intervalSeconds);
                while (mono && mono.isActiveAndEnabled && condition())
                {
                    tick?.Invoke();
                    if (intervalSeconds > 0f)
                        yield return wait;
                    else
                        yield return null;
                }
            }
        }

        /// <summary>
        /// Run tick every frame (or interval) until 'until' becomes true. Then optionally call 'onDone'.
        /// </summary>
        public static Coroutine RunUntil(this MonoBehaviour mono, Func<bool> until, Action tick = null, Action onDone = null, float intervalSeconds = 0f, bool realtime = false)
        {
            if (!mono || until == null) return null;
            intervalSeconds = Mathf.Max(0f, intervalSeconds);
            return mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                var wait = realtime ? (object)new WaitForSecondsRealtime(intervalSeconds) : new WaitForSeconds(intervalSeconds);
                while (mono && mono.isActiveAndEnabled && !until())
                {
                    tick?.Invoke();
                    if (intervalSeconds > 0f)
                        yield return wait;
                    else
                        yield return null;
                }
                onDone?.Invoke();
            }
        }

        /// <summary>
        /// Wait until 'condition' is true, then invoke 'onReady' once and continue calling 'everyFrame' each frame (or interval) 
        /// until 'stopWhen' returns true (if provided).
        /// </summary>
        public static Coroutine RunWhenThenEveryFrame(this MonoBehaviour mono, Func<bool> condition, Action onReady, Action everyFrame, Func<bool> stopWhen = null, float intervalSeconds = 0f, bool realtime = false)
        {
            if (!mono || condition == null) return null;
            intervalSeconds = Mathf.Max(0f, intervalSeconds);

            // If already true, shortcut to looping section
            if (condition())
            {
                onReady?.Invoke();
                return mono.RunWhile(() => stopWhen == null || !stopWhen(), everyFrame, intervalSeconds, realtime);
            }

            return mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                yield return new WaitUntil(condition);
                onReady?.Invoke();
                var wait = realtime ? (object)new WaitForSecondsRealtime(intervalSeconds) : new WaitForSeconds(intervalSeconds);
                while (mono && mono.isActiveAndEnabled && (stopWhen == null || !stopWhen()))
                {
                    everyFrame?.Invoke();
                    if (intervalSeconds > 0f)
                        yield return wait;
                    else
                        yield return null;
                }
            }
        }

        /// <summary>
        /// Run for a fixed number of frames, calling 'eachFrame' every frame, then 'onDone' once.
        /// </summary>
        public static Coroutine RunFrames(this MonoBehaviour mono, int frameCount, Action eachFrame, Action onDone = null)
        {
            if (!mono || frameCount <= 0) return null;
            return mono.StartCoroutineSafe(Routine());

            IEnumerator Routine()
            {
                for (int i = 0; i < frameCount && mono && mono.isActiveAndEnabled; i++)
                {
                    eachFrame?.Invoke();
                    yield return null;
                }
                onDone?.Invoke();
            }
        }

        /// <summary>
        /// Get a component if it exists; otherwise add it.
        /// </summary>
        public static T GetOrAddComponent<T>(this MonoBehaviour mono) where T : Component
        {
            if (!mono) return null;
            var c = mono.GetComponent<T>();
            if (!c) c = mono.gameObject.AddComponent<T>();
            return c;
        }

        /// <summary>
        /// Safely set the active state of the GameObject.
        /// </summary>
        public static void SetActiveSafe(this MonoBehaviour mono, bool active)
        {
            if (!mono) return;
            var go = mono.gameObject;
            if (go && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        /// <summary>
        /// Mark the GameObject so it is not destroyed when loading a new scene.
        /// </summary>
        public static void DontDestroyOnLoadSelf(this MonoBehaviour mono)
        {
            if (!mono) return;
            UnityEngine.Object.DontDestroyOnLoad(mono.gameObject);
        }

        /// <summary>
        /// Destroy all children of the MonoBehaviour's transform.
        /// </summary>
        public static void DestroyChildren(this MonoBehaviour mono, bool immediate = false)
        {
            if (!mono) return;
            var t = mono.transform;
            for (var i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                if (!child) continue;
                var go = child.gameObject;
                if (immediate && !Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(go);
                else
                    UnityEngine.Object.Destroy(go);
            }
        }

        /// <summary>
        /// Safely destroy the GameObject this MonoBehaviour is attached to.
        /// </summary>
        public static void DestroySelfSafe(this MonoBehaviour mono, bool immediate = false)
        {
            if (!mono) return;
            var go = mono.gameObject;
            if (!go) return;
            if (immediate && !Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(go);
            else
                UnityEngine.Object.Destroy(go);
        }
    }
}