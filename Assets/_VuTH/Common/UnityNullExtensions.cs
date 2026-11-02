namespace _VuTH.Common
{
    /// <summary>
    /// Null checks that respect Unity's "fake null".
    /// <para>
    /// A destroyed <see cref="UnityEngine.Object"/> (or a missing serialized reference) is not a real C# null, so
    /// <c>?.</c>, <c>??</c>, <c>??=</c> and <c>is null</c> treat it as alive. This matters most for interface-typed
    /// references (<c>IPoolManager</c>, <c>IAudioManager</c>, …) that are backed by MonoBehaviours, where the
    /// overloaded <c>==</c> cannot be used directly.
    /// </para>
    /// </summary>
    public static class UnityNullExtensions
    {
        /// <summary>
        /// True when <paramref name="obj"/> is non-null and, if it is a Unity object, not destroyed.
        /// </summary>
        public static bool IsAlive(this object obj)
        {
            return obj is UnityEngine.Object unityObject ? unityObject != null : obj != null;
        }

        /// <summary>
        /// Returns <paramref name="obj"/> or a real C# null if it is a destroyed / missing Unity object,
        /// so it is safe to follow with <c>?.</c> / <c>??</c>: <c>_poolManager.OrNull()?.Despawn(go)</c>.
        /// </summary>
        public static T OrNull<T>(this T obj) where T : class
        {
            return obj.IsAlive() ? obj : null;
        }
    }
}
