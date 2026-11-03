using System;

namespace _VuTH.Common.Scene
{
    /// <summary>
    /// Serializable scene reference by name, drawn in the inspector as a dropdown of the scenes in Build Settings
    /// (see <see cref="SceneSelectorAttribute"/>). Converts implicitly to and from <see cref="string"/>, so it can be
    /// passed straight to <c>SceneManager.LoadScene</c>.
    /// </summary>
    [Serializable]
    public struct SceneField : IEquatable<SceneField>
    {
        /// <summary>Scene name (file name without extension). Empty when no scene is selected.</summary>
        [SceneSelector] public string sceneName;

        /// <summary>True when no scene is selected.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(sceneName);

        public static implicit operator string(SceneField field) => field.sceneName;

        public static implicit operator SceneField(string value) => new() { sceneName = value };

        public bool Equals(SceneField other) => string.Equals(sceneName, other.sceneName, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SceneField other && Equals(other);

        public override int GetHashCode() => sceneName != null ? StringComparer.Ordinal.GetHashCode(sceneName) : 0;

        public override string ToString() => sceneName;
    }
}
